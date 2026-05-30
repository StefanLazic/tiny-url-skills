# Stress Test Report — 1000 RPS for 10 Minutes

**Date:** 2026-05-30 22:31
**Environment:** Local (SQLite, Release mode, single k6 instance)
**Test Script:** `tests/stress/stress-test-1000rps.js`
**Configuration:** Constant 1000 RPS for 10 minutes (1:10 create-to-redirect ratio)

---

## Test Parameters

| Parameter | Value |
|-----------|-------|
| Total Target RPS | 1,000 |
| Create RPS (POST /api/shorten) | ~91 |
| Redirect RPS (GET /{slug}) | ~909 |
| Duration | 10 minutes |
| Seed Slugs | 1,000 |

---

## Summary

| Metric | Value |
|--------|-------|
| Total Requests | 599,735 |
| Test Duration | 600s (10.0 min) |
| Overall Throughput | 999.6 RPS |
| Create Requests | 54,601 |
| Redirect Requests | 545,134 |

---

## Latency — Create (`POST /api/shorten`)

| Percentile | Latency |
|------------|---------|
| Median | 1.10 ms |
| p90 | 1.90 ms |
| **p95** | **5.71 ms** |
| p99 | 180.57 ms |
| Average | 7.79 ms |
| Max | 601.81 ms |

---

## Latency — Redirect (`GET /{slug}`)

| Percentile | Latency |
|------------|---------|
| Median | 0.49 ms |
| p90 | 0.67 ms |
| **p95** | **1.92 ms** |
| p99 | 148.25 ms |
| Average | 4.71 ms |
| Max | 481.37 ms |

---

## Requests Over Time

![Requests Over Time](../tests/stress/output/requests_over_time.png)

This graph shows the number of requests executed in each 10-second time bin throughout the 10-minute test. The dashed green line represents total requests, while the blue and orange areas show redirect and create requests respectively. The red dotted line marks the 1000 RPS target.

---

## 95th Percentile Latency Over Time

![p95 Latency Over Time](../tests/stress/output/p95_latency_over_time.png)

This graph shows how the 95th percentile latency evolves over the duration of the test for both create (POST) and redirect (GET) requests. Dotted horizontal lines indicate the threshold targets. Sustained p95 values below the thresholds indicate stable performance at 1000 RPS.

---

## Threshold Results

| Threshold | Target | Result |
|-----------|--------|--------|
| Create p95 latency | < 150 ms | ✅ PASS (5.71 ms) |
| Redirect p95 latency | < 80 ms | ✅ PASS (1.92 ms) |
| Create p99 latency | < 200 ms | ✅ PASS (180.57 ms) |
| Redirect p99 latency | < 100 ms | ❌ FAIL (148.25 ms) |

---

## How This Test Was Run

```bash
# 1. Start the service in Release mode
dotnet run --configuration Release --project src/TinyUrl.Api

# 2. Run the 1000 RPS stress test with CSV output
k6 run --out csv=results.csv tests/stress/stress-test-1000rps.js

# 3. Generate this report
pip install pandas matplotlib
python tests/stress/generate-report.py results.csv
```
