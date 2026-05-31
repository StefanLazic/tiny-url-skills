# Functional Spec: Stress Testing

## Objective

Determine the maximum sustainable throughput of the Tiny URL service in a local environment. "Sustainable" means the highest requests-per-second (RPS) the service can handle while remaining within defined latency and error-rate thresholds.

## Success Criteria

| Metric | Threshold |
|--------|-----------|
| p99 latency — redirect (`GET /{slug}`) | < 100 ms |
| p99 latency — create (`POST /api/shorten`) | < 200 ms |
| HTTP 5xx error rate | < 0.1% |
| Sustained duration at ceiling | ≥ 60 seconds |

The test passes if all thresholds hold simultaneously. The reported ceiling is the highest RPS at which all four criteria are satisfied.

## Workload Model

### Traffic ratio

| Endpoint | Method | Ratio |
|----------|--------|-------|
| `/api/shorten` | POST | 1 |
| `/{slug}` | GET | 10 |

For every 1 create request, there are 10 redirect requests. This models a read-heavy URL shortener where Short URLs are created once and visited many times.

### Endpoints under test

| Endpoint | Purpose | Expected response |
|----------|---------|-------------------|
| `POST /api/shorten` | Create a new Short URL | HTTP 200 + JSON `{ shortUrl, slug }` |
| `GET /{slug}` | Redirect to Original URL | HTTP 301 or 302 (not followed) |

The stats endpoint (`GET /api/{slug}/stats`) is excluded — it shares the same DB lookup pattern as redirect and would not reveal new bottlenecks.

### Redirect behavior

The load test does **not** follow redirects (`maxRedirects: 0`). It measures only the time the service takes to look up the Slug and issue the 301/302 response, isolating the service's own performance from external targets.

## Test Design

### Tool

**k6** (https://k6.io) — chosen for:
- JavaScript-based scenario scripting with native weighted-scenario support
- Built-in threshold assertions (p99, error rate)
- Environment-variable configuration for reuse across local and cloud targets
- No dependency on the .NET runtime (independent from the system under test)

### Seed phase (setup)

Before the main load begins, a `setup()` function creates a pool of 1,000 Slugs by calling `POST /api/shorten` sequentially. These Slugs are returned to the main test phase so that redirect VUs have a warm pool to draw from.

- Seed count: **1,000**
- Original URL used for seeding: `https://example.com` (arbitrary; redirect is not followed)
- Purpose: prevent redirect VUs from starving on non-existent Slugs and avoid hot-cache bias from too few entries

### Ramp strategy

Linear ramp from 50 RPS to 2,000 RPS over 3 minutes. The 1:10 ratio applies throughout:

| Time | Total RPS | Create RPS (approx) | Redirect RPS (approx) |
|------|-----------|---------------------|----------------------|
| 0:00 | 50 | ~5 | ~45 |
| 1:00 | 700 | ~64 | ~636 |
| 2:00 | 1,350 | ~123 | ~1,227 |
| 3:00 | 2,000 | ~182 | ~1,818 |

The point at which any threshold is first breached marks the service's sustainable ceiling. A follow-up confirmation run at ~80% of that ceiling for 60 seconds verifies stability.

### Configuration

| Parameter | Default | Override |
|-----------|---------|----------|
| Base URL | `http://localhost:5000` | `BASE_URL` environment variable |

This allows the same script to target local development or a cloud-hosted deployment without modification.

## Execution Environment

### Service launch

```bash
dotnet run --configuration Release --project src/TinyUrl.Api
```

Release mode removes development middleware overhead (Developer Exception Page, hot reload hooks) and enables compiler optimizations, providing measurements closer to production behavior.

### Database

SQLite (local development default). The seeded 1,000 rows plus rows created during the test remain in the local `tinyurl.db` file. For repeatable runs, delete the database file before each test.

## File Location

```
tests/stress/
├── stress-test.js              ← k6 ramp load test (50 → 2,000 RPS over 3 min)
├── stress-test-1000rps.js      ← k6 constant-rate test (1,000 RPS for 10 min)
├── stress-test-2500rps.js      ← k6 constant-rate test (2,500 RPS for 10 min)
├── stress-test-5000rps.js      ← k6 constant-rate test (5,000 RPS for 10 min)
├── generate-report.py          ← Report + graph generator for 1000 RPS (reads k6 CSV output)
├── generate-report-2500rps.py  ← Report + graph generator for 2500 RPS (reads k6 CSV output)
├── output/                     ← Generated graphs
└── README.md                   ← Installation, usage, and interpretation guide
```

## How to Run

### Ramp test (discover ceiling)

1. Install k6: https://k6.io/docs/get-started/installation/
2. Start the service:
   ```bash
   dotnet run --configuration Release --project src/TinyUrl.Api
   ```
3. Run the stress test:
   ```bash
   k6 run tests/stress/stress-test.js
   ```
4. Target a different environment:
   ```bash
   BASE_URL=https://api.myservice.com k6 run tests/stress/stress-test.js
   ```

### 1000 RPS sustained test (10 minutes)

1. Start the service (same as above)
2. Run the constant-rate test with CSV output:
   ```bash
   k6 run --out csv=results.csv tests/stress/stress-test-1000rps.js
   ```
3. Generate the report with graphs:
   ```bash
   pip install pandas matplotlib
   python tests/stress/generate-report.py results.csv
   ```
4. View the generated report at `docs/stress-test-report-1000rps.md` and graphs in `tests/stress/output/`

## Interpreting Results

- k6 outputs a summary table with p50, p95, p99 latencies and request counts per scenario.
- Built-in threshold checks report PASS or FAIL automatically.
- The RPS value at which thresholds first break is the sustainable ceiling.
- After discovering the ceiling, run a 60-second constant-rate confirmation at ~80% of that value to verify sustained stability.

## Out of Scope

- Distributed load generation (single local k6 instance only)
- Stats endpoint testing
- Following redirects to external targets
- Database-level profiling or APM instrumentation
- Production environment benchmarking (covered by reusing the script with `BASE_URL` override, but not part of this spec)
