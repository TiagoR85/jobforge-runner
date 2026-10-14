# ADR-0001: Inline retry loop in the consumer

**Status:** Accepted
**Date:** 2026-10-14

## Decision

Retry failed webhook executions inline in the Kafka consumer with exponential backoff (`2^(attempt-1)` seconds, capped at 30s per backoff). Attempt state lives in the message (`MaxRetries`, current attempt tracked in the processing loop); the message is not re-published between retries.

## Context

The runner must retry transient failures (5xx, 408, 429, timeouts, network errors) up to `MaxRetries` (default 5) before dead-lettering. The maximum backoff is 30s, well under Kafka's default `max.poll.interval.ms` of 300s, so the consumer can safely sleep between attempts on the same message.

## Consequences

- Simple: no extra topics, producers, or schedulers.
- The partition is blocked during retries (acceptable at this scale; one message in-flight per partition).
- A crash mid-retry with the Redis dedup key set skips remaining attempts (see ADR-0002).

## Rejected alternatives

- **Retry topics** (one per delay class or a single delayed retry topic): extra topics + producers per retry class, operational noise disproportionate to the scale of this study project.
- **Sleep-in-consumer without cap**: unbounded backoff could exceed `max.poll.interval.ms` and cause rebalances.
