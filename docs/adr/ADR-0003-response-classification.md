# ADR-0003: HTTP response classification (4xx permanent vs 5xx retryable)

**Status:** Accepted
**Date:** 2026-10-14

## Decision

Classify webhook responses as follows:

- **2xx / 3xx** → `succeeded`
- **4xx except 408/429** → `permanent` (dead-letter immediately after first attempt)
- **5xx / 408 / 429 / timeout / network error** → `retryable` (inline retry with backoff)

## Context

Spec §9. A 404 or 422 will never become a 200 by retrying; retrying them wastes resources and delays the dead-letter signal. 408 (request timeout) and 429 (rate limit) are transient by definition and must be retried. 5xx indicates server-side issues that may be temporary.

## Consequences

- Permanent failures reach the DLQ (and `dead` status) quickly.
- Transient failures get up to `MaxRetries` attempts with exponential backoff.

## Rejected alternatives

- **"All 4xx retry"**: a 404 will never become a 200; retrying wastes attempts and delays operator visibility.
- **"Treat 429 as dead"**: rate limits are transient by definition; retrying after backoff is the correct response.
