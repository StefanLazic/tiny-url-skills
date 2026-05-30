# Stress Testing

Load test for the Tiny URL service using [k6](https://k6.io).

## Prerequisites

Install k6: https://k6.io/docs/get-started/installation/

```bash
# macOS
brew install k6

# Windows (chocolatey)
choco install k6

# Linux (Debian/Ubuntu)
sudo gpg -k
sudo gpg --no-default-keyring --keyring /usr/share/keyrings/k6-archive-keyring.gpg --keyserver hkp://keyserver.ubuntu.com:80 --recv-keys C5AD17C747E3415A3642D57D77C6C491D6AC1D68
echo "deb [signed-by=/usr/share/keyrings/k6-archive-keyring.gpg] https://dl.k6.io/deb stable main" | sudo tee /etc/apt/sources.list.d/k6.list
sudo apt-get update
sudo apt-get install k6
```

## Running the Test

### 1. Start the service in Release mode

```bash
dotnet run --configuration Release --project src/TinyUrl.Api
```

### 2. Run the stress test

```bash
k6 run tests/stress/stress-test.js
```

### 3. Target a different environment

```bash
BASE_URL=https://api.myservice.com k6 run tests/stress/stress-test.js
```

## What the Test Does

1. **Seed phase** — Creates 1,000 Short URLs via `POST /api/shorten` to build a Slug pool.
2. **Main phase** — Ramps from 50 to 2,000 total RPS over 3 minutes with a 1:10 create-to-redirect ratio:
   - `POST /api/shorten` ramps from ~5 to ~182 RPS
   - `GET /{slug}` ramps from ~45 to ~1,818 RPS (does not follow redirects)

## Success Criteria

| Metric | Threshold |
|--------|-----------|
| p99 latency — redirect (`GET /{slug}`) | < 100 ms |
| p99 latency — create (`POST /api/shorten`) | < 200 ms |
| HTTP error rate (5xx) | < 0.1% |

k6 reports PASS/FAIL for each threshold automatically in the output summary.

## Interpreting Results

The k6 summary shows:

- **http_req_duration** — Latency percentiles (p50, p90, p95, p99) per scenario
- **http_reqs** — Total requests made and RPS achieved
- **http_req_failed** — Percentage of failed requests
- **checks** — Pass/fail counts for status code assertions
- **thresholds** — ✓ or ✗ for each defined threshold

The **sustainable ceiling** is the RPS at which all thresholds still pass. Look at the k6 output timeline or use `--out csv=results.csv` for detailed per-second data.

## Confirmation Run

After finding the ceiling, confirm stability with a constant-rate run at ~80% of the discovered value. Edit the script stages to hold a flat rate for 60 seconds, or run a quick test:

```bash
k6 run --duration 60s --rps <80_percent_of_ceiling> tests/stress/stress-test.js
```

## Troubleshooting

- **Connection refused** — Ensure the service is running on the expected port (default: 5000).
- **All requests fail in setup** — Check that the API is responding at `BASE_URL/api/shorten`.
- **Low RPS achieved** — k6 may need more VUs. Increase `maxVUs` in the script if your machine can handle it.
- **SQLite lock errors under high load** — Expected at very high write concurrency; this is a known SQLite limitation and represents the local ceiling for write-heavy workloads.
