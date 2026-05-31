"""
Stress Test Report Generator — 2500 RPS

Reads k6 CSV output and generates:
1. A requests-over-time graph (X: time, Y: number of requests executed)
2. A p95 latency graph for create and redirect requests over time
3. A markdown report with embedded graphs

Usage:
  1. Run the k6 stress test with CSV output:
     k6 run --out csv=results-2500rps.csv tests/stress/stress-test-2500rps.js

  2. Generate the report:
     python tests/stress/generate-report-2500rps.py results-2500rps.csv

  The script produces:
    - tests/stress/output/requests_over_time_2500rps.png
    - tests/stress/output/p95_latency_over_time_2500rps.png
    - docs/stress-test-report-2500rps.md

Requirements:
  pip install pandas matplotlib
"""

import sys
import os
import pandas as pd
import matplotlib.pyplot as plt
import numpy as np
from datetime import datetime

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
REPO_ROOT = os.path.abspath(os.path.join(SCRIPT_DIR, "..", ".."))
OUTPUT_DIR = os.path.join(SCRIPT_DIR, "output")

TARGET_RPS = 2500
CREATE_RPS = 227
REDIRECT_RPS = 2273


def parse_k6_csv(csv_path):
    """Parse k6 CSV output into a DataFrame."""
    df = pd.read_csv(csv_path, low_memory=False)
    df["timestamp"] = pd.to_datetime(df["timestamp"], unit="s")
    return df


def filter_http_duration(df):
    """Filter for http_req_duration metric rows only, excluding setup phase."""
    filtered = df[df["metric_name"] == "http_req_duration"].copy()
    filtered = filtered[filtered["group"] != "::setup"]
    return filtered


def classify_scenario(row):
    """Classify a row as 'create' or 'redirect' based on scenario column."""
    scenario = str(row.get("scenario", ""))
    if "create" in scenario:
        return "create"
    elif "redirect" in scenario:
        return "redirect"
    return "unknown"


def compute_requests_over_time(df, bin_seconds=10):
    """Compute request counts per time bin."""
    df = df.copy()
    start = df["timestamp"].min()
    df["elapsed_seconds"] = (df["timestamp"] - start).dt.total_seconds()
    df["time_bin"] = (df["elapsed_seconds"] // bin_seconds) * bin_seconds

    counts = df.groupby("time_bin").size().reset_index(name="request_count")
    counts["rps"] = counts["request_count"] / bin_seconds

    df["scenario"] = df.apply(classify_scenario, axis=1)
    create_counts = (
        df[df["scenario"] == "create"]
        .groupby("time_bin")
        .size()
        .reset_index(name="count")
    )
    redirect_counts = (
        df[df["scenario"] == "redirect"]
        .groupby("time_bin")
        .size()
        .reset_index(name="count")
    )

    return counts, create_counts, redirect_counts


def compute_p95_over_time(df, bin_seconds=10):
    """Compute p95 latency per time bin for each scenario."""
    df = df.copy()
    start = df["timestamp"].min()
    df["elapsed_seconds"] = (df["timestamp"] - start).dt.total_seconds()
    df["time_bin"] = (df["elapsed_seconds"] // bin_seconds) * bin_seconds
    df["scenario"] = df.apply(classify_scenario, axis=1)

    create_p95 = (
        df[df["scenario"] == "create"]
        .groupby("time_bin")["metric_value"]
        .quantile(0.95)
        .reset_index(name="p95_ms")
    )
    redirect_p95 = (
        df[df["scenario"] == "redirect"]
        .groupby("time_bin")["metric_value"]
        .quantile(0.95)
        .reset_index(name="p95_ms")
    )

    return create_p95, redirect_p95


def format_time_label(seconds):
    """Format seconds as MM:SS."""
    minutes = int(seconds // 60)
    secs = int(seconds % 60)
    return f"{minutes}:{secs:02d}"


def plot_requests_over_time(total_counts, create_counts, redirect_counts, output_path):
    """Generate the requests-over-time graph."""
    fig, ax = plt.subplots(figsize=(14, 6))

    ax.fill_between(
        redirect_counts["time_bin"],
        redirect_counts["count"],
        alpha=0.3,
        color="#2196F3",
        label="Redirect (GET /{slug})",
    )
    ax.plot(
        redirect_counts["time_bin"],
        redirect_counts["count"],
        color="#2196F3",
        linewidth=1,
    )

    ax.fill_between(
        create_counts["time_bin"],
        create_counts["count"],
        alpha=0.3,
        color="#FF9800",
        label="Create (POST /api/shorten)",
    )
    ax.plot(
        create_counts["time_bin"],
        create_counts["count"],
        color="#FF9800",
        linewidth=1,
    )

    ax.plot(
        total_counts["time_bin"],
        total_counts["request_count"],
        color="#4CAF50",
        linewidth=2,
        linestyle="--",
        label="Total Requests",
    )

    # Target line at 25,000 per 10s bin (= 2500 RPS)
    ax.axhline(y=TARGET_RPS * 10, color="red", linestyle=":", alpha=0.5, label=f"Target: {TARGET_RPS} RPS")

    ax.set_xlabel("Time (MM:SS)", fontsize=12)
    ax.set_ylabel("Number of Requests (per 10s bin)", fontsize=12)
    ax.set_title(f"Requests Executed Over Time — {TARGET_RPS} RPS Stress Test", fontsize=14)
    ax.legend(loc="upper left", fontsize=10)
    ax.grid(True, alpha=0.3)

    tick_positions = np.linspace(total_counts["time_bin"].min(), total_counts["time_bin"].max(), 11)
    ax.set_xticks(tick_positions)
    ax.set_xticklabels([format_time_label(t) for t in tick_positions])

    plt.tight_layout()
    plt.savefig(output_path, dpi=150, bbox_inches="tight")
    plt.close()
    print(f"Saved: {output_path}")


def plot_p95_latency(create_p95, redirect_p95, output_path):
    """Generate the p95 latency over time graph."""
    fig, ax = plt.subplots(figsize=(14, 6))

    ax.plot(
        create_p95["time_bin"],
        create_p95["p95_ms"],
        color="#FF9800",
        linewidth=2,
        marker="o",
        markersize=3,
        label="Create p95 (POST /api/shorten)",
    )

    ax.plot(
        redirect_p95["time_bin"],
        redirect_p95["p95_ms"],
        color="#2196F3",
        linewidth=2,
        marker="s",
        markersize=3,
        label="Redirect p95 (GET /{slug})",
    )

    ax.axhline(y=150, color="#FF9800", linestyle=":", alpha=0.5, label="Create p95 threshold (150ms)")
    ax.axhline(y=80, color="#2196F3", linestyle=":", alpha=0.5, label="Redirect p95 threshold (80ms)")

    ax.set_xlabel("Time (MM:SS)", fontsize=12)
    ax.set_ylabel("p95 Latency (ms)", fontsize=12)
    ax.set_title(f"95th Percentile Latency Over Time — {TARGET_RPS} RPS Stress Test", fontsize=14)
    ax.legend(loc="upper left", fontsize=10)
    ax.grid(True, alpha=0.3)

    all_bins = pd.concat([create_p95["time_bin"], redirect_p95["time_bin"]])
    tick_positions = np.linspace(all_bins.min(), all_bins.max(), 11)
    ax.set_xticks(tick_positions)
    ax.set_xticklabels([format_time_label(t) for t in tick_positions])

    plt.tight_layout()
    plt.savefig(output_path, dpi=150, bbox_inches="tight")
    plt.close()
    print(f"Saved: {output_path}")


def compute_summary_stats(df):
    """Compute overall summary statistics."""
    df = df.copy()
    df["scenario"] = df.apply(classify_scenario, axis=1)
    duration_seconds = (df["timestamp"].max() - df["timestamp"].min()).total_seconds()

    stats = {
        "total_requests": len(df),
        "duration_seconds": duration_seconds,
        "overall_rps": len(df) / duration_seconds if duration_seconds > 0 else 0,
    }

    for scenario in ["create", "redirect"]:
        s_df = df[df["scenario"] == scenario]["metric_value"]
        stats[f"{scenario}_count"] = len(s_df)
        stats[f"{scenario}_avg"] = s_df.mean()
        stats[f"{scenario}_median"] = s_df.median()
        stats[f"{scenario}_p90"] = s_df.quantile(0.90)
        stats[f"{scenario}_p95"] = s_df.quantile(0.95)
        stats[f"{scenario}_p99"] = s_df.quantile(0.99)
        stats[f"{scenario}_max"] = s_df.max()

    return stats


def generate_markdown_report(stats, graphs_dir, output_path):
    """Generate the markdown report."""
    rel_graphs = os.path.relpath(graphs_dir, os.path.dirname(output_path))

    report = f"""# Stress Test Report — {TARGET_RPS} RPS for 10 Minutes

**Date:** {datetime.now().strftime("%Y-%m-%d %H:%M")}
**Environment:** Local (SQLite, Release mode, single k6 instance)
**Test Script:** `tests/stress/stress-test-2500rps.js`
**Configuration:** Constant {TARGET_RPS} RPS for 10 minutes (1:10 create-to-redirect ratio)

---

## Test Parameters

| Parameter | Value |
|-----------|-------|
| Total Target RPS | {TARGET_RPS:,} |
| Create RPS (POST /api/shorten) | ~{CREATE_RPS} |
| Redirect RPS (GET /{{slug}}) | ~{REDIRECT_RPS} |
| Duration | 10 minutes |
| Seed Slugs | 1,000 |

---

## Summary

| Metric | Value |
|--------|-------|
| Total Requests | {stats['total_requests']:,} |
| Test Duration | {stats['duration_seconds']:.0f}s ({stats['duration_seconds']/60:.1f} min) |
| Overall Throughput | {stats['overall_rps']:.1f} RPS |
| Create Requests | {stats['create_count']:,} |
| Redirect Requests | {stats['redirect_count']:,} |

---

## Latency — Create (`POST /api/shorten`)

| Percentile | Latency |
|------------|---------|
| Median | {stats['create_median']:.2f} ms |
| p90 | {stats['create_p90']:.2f} ms |
| **p95** | **{stats['create_p95']:.2f} ms** |
| p99 | {stats['create_p99']:.2f} ms |
| Average | {stats['create_avg']:.2f} ms |
| Max | {stats['create_max']:.2f} ms |

---

## Latency — Redirect (`GET /{{slug}}`)

| Percentile | Latency |
|------------|---------|
| Median | {stats['redirect_median']:.2f} ms |
| p90 | {stats['redirect_p90']:.2f} ms |
| **p95** | **{stats['redirect_p95']:.2f} ms** |
| p99 | {stats['redirect_p99']:.2f} ms |
| Average | {stats['redirect_avg']:.2f} ms |
| Max | {stats['redirect_max']:.2f} ms |

---

## Requests Over Time

![Requests Over Time]({rel_graphs}/requests_over_time_2500rps.png)

This graph shows the number of requests executed in each 10-second time bin throughout the 10-minute test. The dashed green line represents total requests, while the blue and orange areas show redirect and create requests respectively. The red dotted line marks the {TARGET_RPS} RPS target.

---

## 95th Percentile Latency Over Time

![p95 Latency Over Time]({rel_graphs}/p95_latency_over_time_2500rps.png)

This graph shows how the 95th percentile latency evolves over the duration of the test for both create (POST) and redirect (GET) requests. Dotted horizontal lines indicate the threshold targets. Sustained p95 values below the thresholds indicate stable performance at {TARGET_RPS} RPS.

---

## Threshold Results

| Threshold | Target | Result |
|-----------|--------|--------|
| Create p95 latency | < 150 ms | {"✅ PASS" if stats['create_p95'] < 150 else "❌ FAIL"} ({stats['create_p95']:.2f} ms) |
| Redirect p95 latency | < 80 ms | {"✅ PASS" if stats['redirect_p95'] < 80 else "❌ FAIL"} ({stats['redirect_p95']:.2f} ms) |
| Create p99 latency | < 200 ms | {"✅ PASS" if stats['create_p99'] < 200 else "❌ FAIL"} ({stats['create_p99']:.2f} ms) |
| Redirect p99 latency | < 100 ms | {"✅ PASS" if stats['redirect_p99'] < 100 else "❌ FAIL"} ({stats['redirect_p99']:.2f} ms) |

---

## How This Test Was Run

```bash
# 1. Start the service in Release mode
dotnet run --configuration Release --project src/TinyUrl.Api

# 2. Run the {TARGET_RPS} RPS stress test with CSV output
k6 run --out csv=results-2500rps.csv tests/stress/stress-test-2500rps.js

# 3. Generate this report
pip install pandas matplotlib
python tests/stress/generate-report-2500rps.py results-2500rps.csv
```
"""
    os.makedirs(os.path.dirname(output_path), exist_ok=True)
    with open(output_path, "w") as f:
        f.write(report)
    print(f"Saved: {output_path}")


def main():
    if len(sys.argv) < 2:
        print("Usage: python generate-report-2500rps.py <k6-results.csv>")
        print("")
        print("First run the stress test with CSV output:")
        print("  k6 run --out csv=results-2500rps.csv tests/stress/stress-test-2500rps.js")
        print("")
        print("Then generate the report:")
        print("  python tests/stress/generate-report-2500rps.py results-2500rps.csv")
        sys.exit(1)

    csv_path = sys.argv[1]
    if not os.path.exists(csv_path):
        print(f"Error: File not found: {csv_path}")
        sys.exit(1)

    os.makedirs(OUTPUT_DIR, exist_ok=True)

    print(f"Parsing {csv_path}...")
    df = parse_k6_csv(csv_path)
    http_df = filter_http_duration(df)

    print(f"Found {len(http_df):,} http_req_duration samples")

    # Compute data
    total_counts, create_counts, redirect_counts = compute_requests_over_time(http_df)
    create_p95, redirect_p95 = compute_p95_over_time(http_df)
    stats = compute_summary_stats(http_df)

    # Generate graphs
    requests_graph = os.path.join(OUTPUT_DIR, "requests_over_time_2500rps.png")
    p95_graph = os.path.join(OUTPUT_DIR, "p95_latency_over_time_2500rps.png")

    plot_requests_over_time(total_counts, create_counts, redirect_counts, requests_graph)
    plot_p95_latency(create_p95, redirect_p95, p95_graph)

    # Generate report
    report_path = os.path.join(REPO_ROOT, "docs", "stress-test-report-2500rps.md")
    generate_markdown_report(stats, OUTPUT_DIR, report_path)

    print("\nDone! Report and graphs generated successfully.")
    print(f"  Report: {report_path}")
    print(f"  Graphs: {OUTPUT_DIR}/")


if __name__ == "__main__":
    main()
