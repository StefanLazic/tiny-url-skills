# Tiny URL — Functional Specification

## 1. Overview

A URL shortening service that accepts an Original URL and returns a Short URL containing a unique Slug. When a user visits the Short URL, they are redirected to the Original URL.

## 2. Terminology

| Term         | Definition                                              |
|--------------|---------------------------------------------------------|
| Original URL | The full-length URL submitted by the user.              |
| Short URL    | The full shortened link that users share.               |
| Slug         | The unique short code that appears in the Short URL.    |

**Avoid:** "long URL", "link", "tiny URL", "redirect URL".

## 3. Tech Stack

| Layer              | Technology                          |
|--------------------|-------------------------------------|
| Language/Framework | C# / ASP.NET Core                   |
| ORM                | Entity Framework Core               |
| Database (local)   | SQLite                              |
| Database (prod)    | PostgreSQL                          |

EF Core's provider abstraction is used to swap between SQLite (development/testing) and PostgreSQL (production) based on the environment configuration.

## 4. Core Features

### 4.1 Create Short URL

- **Input:** An Original URL and an optional custom Slug.
- **Output:** A newly created Short URL.
- **Slug generation:** Random base62 string by default. Users may optionally provide their own custom Slug.
- **Deduplication:** None. Submitting the same Original URL multiple times always generates a new Slug each time.

### 4.2 Redirect

- **Input:** A Short URL (containing a Slug).
- **Behavior:** Looks up the Slug, increments the click counter, and issues an HTTP redirect to the corresponding Original URL.
- **Expired URLs:** If the URL has passed its `expires_at` timestamp, return **HTTP 410 Gone** instead of redirecting.

### 4.3 Expiration

- Short URLs **never expire by default**.
- An optional `expires_at` timestamp can be set at creation time.
- Once expired, the Short URL returns HTTP 410 Gone.

### 4.4 Analytics

- **Basic click count only.**
- A simple integer counter is incremented on each redirect.
- No PII is collected. No detailed logging (referrer, user-agent, geo, etc.).

## 5. Data Model

| Field        | Type              | Description                                    |
|--------------|-------------------|------------------------------------------------|
| Id           | GUID / int        | Primary key.                                   |
| Slug         | string (unique)   | The short code in the URL.                     |
| OriginalUrl  | string            | The full-length destination URL.               |
| ClickCount   | int               | Number of times the Short URL has been visited.|
| ExpiresAt    | DateTime? (nullable) | Optional expiration timestamp.              |
| CreatedAt    | DateTime          | When the Short URL was created.                |

## 6. API Endpoints (Planned)

| Method | Path          | Description                        |
|--------|---------------|------------------------------------|
| POST   | /api/shorten  | Create a new Short URL.            |
| GET    | /{slug}       | Redirect to the Original URL.      |
| GET    | /api/{slug}/stats | Get click count for a Slug.    |

## 7. Environment Configuration

- **Development/Testing:** Uses SQLite. Database file stored locally. EF Core migrations applied automatically on startup.
- **Production:** Uses PostgreSQL. Connection string provided via environment variables or secrets management.
