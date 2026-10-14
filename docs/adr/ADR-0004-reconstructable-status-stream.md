# ADR-0004: Reconstructable status stream (Postgres state derived from gRPC)

**Status:** Accepted
**Date:** 2026-10-14

## Decision

The orchestrator's Postgres state is derived exclusively from the runner's gRPC status stream. The runner holds one duplex stream per instance (`ExecutionStatus.Report`), with reconnect backoff (1s..30s) and a pending-report requeue: if an ack reports `persisted=false` or the send fails, the report is requeued and re-sent after reconnect.

## Context

Spec §10. The orchestrator is the source of truth for job/execution records, but the runner is the source of truth for execution *events*. Keeping the state derived from a single stream simplifies both sides: the orchestrator applies idempotent updates; the runner guarantees delivery via requeue.

## Consequences

- Orchestrator crash loses nothing durable — the runner re-reports on reconnect.
- No heartbeats needed; the stream itself is the liveness signal (plus `/health/ready` on the runner).
- Ordering within one execution is preserved (single in-flight report per stream).

## Rejected alternatives

- **Per-report unary calls**: chattier, no backpressure story, harder to guarantee ordering.
- **Brokered state** (events through Kafka for status too): another moving part; the gRPC stream is already point-to-point and low-volume.
