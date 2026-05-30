import http from "k6/http";
import { check } from "k6";
import { SharedArray } from "k6/data";

const BASE_URL = __ENV.BASE_URL || "http://localhost:5000";
const SEED_COUNT = 1000;

export const options = {
  scenarios: {
    create_scenario: {
      executor: "ramping-arrival-rate",
      exec: "createShortUrl",
      startRate: 5,
      timeUnit: "1s",
      preAllocatedVUs: 50,
      maxVUs: 200,
      stages: [
        { duration: "3m", target: 182 }, // ramp to ~182 create RPS (1/11 of 2000)
      ],
    },
    redirect_scenario: {
      executor: "ramping-arrival-rate",
      exec: "redirectToOriginal",
      startRate: 45,
      timeUnit: "1s",
      preAllocatedVUs: 200,
      maxVUs: 2000,
      stages: [
        { duration: "3m", target: 1818 }, // ramp to ~1818 redirect RPS (10/11 of 2000)
      ],
    },
  },
  thresholds: {
    "http_req_duration{scenario:redirect_scenario}": ["p(99)<100"],
    "http_req_duration{scenario:create_scenario}": ["p(99)<200"],
    http_req_failed: ["rate<0.001"],
  },
};

export function setup() {
  const slugs = [];

  for (let i = 0; i < SEED_COUNT; i++) {
    const payload = JSON.stringify({
      originalUrl: `https://example.com/seed/${i}`,
    });

    const res = http.post(`${BASE_URL}/api/shorten`, payload, {
      headers: { "Content-Type": "application/json" },
    });

    if (res.status === 200) {
      const body = JSON.parse(res.body);
      slugs.push(body.slug);
    }
  }

  if (slugs.length < 900) {
    throw new Error(
      `Seeding failed: only ${slugs.length}/${SEED_COUNT} Slugs created. Aborting test.`
    );
  }

  console.log(`Seeded ${slugs.length} Slugs`);
  return { slugs };
}

export function createShortUrl() {
  const payload = JSON.stringify({
    originalUrl: `https://example.com/${Date.now()}/${Math.random()}`,
  });

  const res = http.post(`${BASE_URL}/api/shorten`, payload, {
    headers: { "Content-Type": "application/json" },
  });

  check(res, {
    "create: status is 200": (r) => r.status === 200,
  });
}

export function redirectToOriginal(data) {
  const slugs = data.slugs;
  const slug = slugs[Math.floor(Math.random() * slugs.length)];

  const res = http.get(`${BASE_URL}/${slug}`, {
    redirects: 0,
  });

  check(res, {
    "redirect: status is 301 or 302": (r) =>
      r.status === 301 || r.status === 302,
  });
}
