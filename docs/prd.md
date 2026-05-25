# PRD: Tiny URL — Core Service Implementation

## Problem Statement

Developers and users need a fast, simple URL shortening service. Long URLs are cumbersome to share, track, or embed. There is currently no service in this repository to shorten Original URLs into Short URLs backed by unique Slugs, redirect visitors to the original destination, or report basic click analytics.

## Solution

Build a URL shortening service as an ASP.NET Core API backed by EF Core (SQLite in development, PostgreSQL in production). The service allows a user to submit an Original URL and receive a Short URL. Visiting the Short URL redirects the user to the Original URL. The service tracks a basic click counter per Short URL and supports optional expiration.

## User Stories

1. As an API consumer, I want to submit an Original URL and receive a Short URL, so that I can share a compact link instead of the full URL.
2. As an API consumer, I want to optionally supply my own custom Slug when creating a Short URL, so that I can produce memorable or branded links.
3. As an API consumer, I want a random base62 Slug to be auto-generated when I do not provide a custom Slug, so that I can create Short URLs without thinking about naming.
4. As an API consumer, I want submitting the same Original URL multiple times to always produce a new Slug, so that I can create independent Short URLs for tracking purposes.
5. As a user, I want to visit a Short URL and be redirected to the Original URL, so that I can reach my destination without knowing the full URL.
6. As a user, I want to receive HTTP 301/302 when visiting a valid Short URL, so that my browser follows the redirect automatically.
7. As a user, I want to receive HTTP 410 Gone when visiting an expired Short URL, so that I know the link is no longer valid rather than being silently misdirected.
8. As an API consumer, I want to optionally set an `expires_at` timestamp when creating a Short URL, so that the link automatically stops working after a certain date and time.
9. As an API consumer, I want Short URLs to never expire by default, so that links remain active indefinitely unless I explicitly set an expiration.
10. As an API consumer, I want to retrieve the click count for a given Slug, so that I can see how many times a Short URL has been visited.
11. As an API consumer, I want click counts to increment automatically on every redirect, so that analytics are kept without any extra effort on my part.
12. As an operator, I want the service to use SQLite in development and testing environments, so that I can run the service locally without any external database.
13. As an operator, I want the service to use PostgreSQL in production, so that the service is backed by a robust, scalable database.
14. As an operator, I want EF Core migrations to be applied automatically on startup in development, so that I do not have to manually manage the schema during development.
15. As an operator, I want the production database connection string to be supplied via environment variables or secrets management, so that credentials are never stored in source code.

## Implementation Decisions

### Modules

- **Slug Generator** — Encapsulates slug generation logic. Generates a random base62 string of a fixed length by default. Accepts a caller-supplied custom slug and validates that it is non-empty and URL-safe. Returns the final slug string. No external dependencies; trivially testable in isolation.

- **URL Repository** — Abstracts all persistence operations for Short URL records. Backed by EF Core with a `DbContext` configured per environment (SQLite or PostgreSQL via the provider abstraction). Exposes: create a record, look up by slug, increment click count, and fetch stats by slug.

- **Shorten URL Use Case** — Orchestrates creation of a new Short URL. Calls the Slug Generator (custom or random), writes the record via the URL Repository, and returns the full Short URL string. Validates the Original URL format before persisting.

- **Redirect Use Case** — Looks up a slug via the URL Repository, checks the `expires_at` field against the current timestamp, increments `ClickCount` if not expired, and returns either the Original URL (for redirect) or a 410 Gone signal.

- **Stats Use Case** — Fetches `ClickCount` for a given slug via the URL Repository and returns it to the caller.

- **API Layer** — ASP.NET Core minimal API or controller endpoints that wire the three use cases to HTTP:
  - `POST /api/shorten` — calls Shorten URL Use Case
  - `GET /{slug}` — calls Redirect Use Case and issues the HTTP redirect or 410
  - `GET /api/{slug}/stats` — calls Stats Use Case and returns JSON

### Data Model

| Field       | Type                  | Notes                              |
|-------------|-----------------------|------------------------------------|
| Id          | GUID or int           | Primary key                        |
| Slug        | string (unique index) | Short code; max ~12 chars          |
| OriginalUrl | string                | Full destination URL               |
| ClickCount  | int                   | Defaults to 0                      |
| ExpiresAt   | DateTime? (nullable)  | Null means no expiration           |
| CreatedAt   | DateTime              | Set at creation time (UTC)         |

### API Contracts

- **POST /api/shorten**
  - Request body: `{ "originalUrl": "...", "customSlug": "..." (optional), "expiresAt": "..." (optional, ISO 8601) }`
  - Response: `{ "shortUrl": "...", "slug": "..." }`
  - Returns HTTP 400 for invalid Original URL or conflicting custom slug.

- **GET /{slug}**
  - Returns HTTP 301/302 to Original URL on success.
  - Returns HTTP 404 if slug is not found.
  - Returns HTTP 410 Gone if the URL has expired.

- **GET /api/{slug}/stats**
  - Response: `{ "slug": "...", "clickCount": N }`
  - Returns HTTP 404 if slug is not found.

### Architectural Decisions

- A single `AppDbContext` uses SQLite in `Development`/`Test` and PostgreSQL in `Production`, driven by `ASPNETCORE_ENVIRONMENT`.
- Migrations run automatically on startup in Development; in Production they are applied via a separate migration step (not on startup).
- No deduplication: every POST always produces a new Slug, even for the same Original URL.
- No PII collection: only the integer click counter is incremented on redirect.

## Testing Decisions

- **What makes a good test:** Tests assert observable external behavior only — HTTP status codes, response body shapes, and side effects visible through the public API (e.g., changed click count). Tests must not assert on internal method calls, private fields, or EF Core internals.

- **Modules to test:**
  - **Slug Generator** — Unit tests: random output length and character set, custom slug pass-through, invalid slug rejection.
  - **Shorten URL Use Case** — Integration tests with SQLite test DB: correct field values, base62 auto-slug, custom slug stored verbatim, `expires_at` persisted correctly.
  - **Redirect Use Case** — Integration tests: redirect + counter increment, 410 for expired, 404 for unknown.
  - **Stats Use Case** — Integration tests: correct click count returned.
  - **API Layer** — End-to-end tests via `WebApplicationFactory<Program>`: all three endpoints with valid/invalid inputs; assert status codes and response body shape.

- **Prior art:** No existing tests. The first tests written will establish the pattern: xUnit with `WebApplicationFactory<Program>` for HTTP-level tests and plain xUnit for Slug Generator unit tests.

## Out of Scope

- User authentication or authorization.
- URL deduplication.
- Detailed analytics (referrer, user-agent, geo, time-series).
- URL editing or deletion after creation.
- Custom or vanity domains.
- Rate limiting or abuse prevention.
- Admin UI or management dashboard.
- Batch URL shortening.

## Further Notes

- Terminology must be consistent throughout the codebase: use **Original URL**, **Short URL**, and **Slug**. Avoid "long URL", "link", "tiny URL", and "redirect URL".
- The base62 alphabet is `[0-9A-Za-z]`; slug length should be configurable but default to 7–8 characters for a reasonable collision budget.
- `expires_at` comparisons must use UTC timestamps throughout to avoid timezone ambiguity.
