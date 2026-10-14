# ADR-0002: Redis SET NX dedup as exactly-once-effective guard

**Status:** Accepted
**Date:** 2026-10-14

## Decision

Guard against duplicate execution with `SET NX runner:exec:{executionId} EX 3600` in Redis before processing each message. If the key already exists, the message is acknowledged (offset committed) and skipped.

## Context

Kafka guarantees at-least-once delivery. After a rebalance or crash, the same message can be redelivered. The runner must not execute the same webhook twice within the dedup window. The runner has no Postgres (by service boundary), so the guard cannot live in the orchestrator's database.

## Consequences

- Exactly-once-effectively within the 1h TTL window.
- Crash mid-retry with the key set skips remaining attempts: the status stays `failed` until a human or CLI replays the execution. Documented trade-off.
- Redis is a hard dependency for correctness (health-checked on `/health/ready`).

## Rejected alternatives

- **DB unique index in the orchestrator**: runner has no Postgres connection by design (service boundary).
- **Kafka idempotent producer alone**: protects the producer side, not consumer redelivery after a rebalance.
