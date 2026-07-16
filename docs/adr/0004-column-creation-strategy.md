# Title
Column creation strategy

# Date
2026-07-15

## Context
`CreateColumnRequest.Position` is an optional `int?` with undefined behavior for null, negative, zero, or gapped values (e.g. `44` when only 3 columns exist).
We also need a policy for concurrent creates that collide on the same position. This ADR covers both: how a requested position is interpreted, and how creation conflicts are resolved.

## Considered Options: normalizing the requested position

### Strict validation
Reject any `Position` outside `[1, count + 1]` with a 400 validation error.
Simple to implement, but forces the caller to know the board's exact column count first.

### Sparse / gap-tolerant
Store whatever positive integer is supplied, gaps included, with no shifting.
Avoids touching other rows, but pushes collision-avoidance onto the caller and lets `Position` drift from a 1-based ordinal into an opaque ranking number, complicating a future reorder endpoint.

### Normalize & shift
Omitted `Position` appends at the end (`max + 1`).
Any supplied value is clamped into `[1, count + 1]`; out-of-range values degrade to "append" instead of erroring, and columns at or after the target slot shift up by one.
Keeps `Position` a dense `1..N` ordinal, matching how `BoardService` already orders columns, and callers never need to know the current column count.
Creation is no longer a single-row insert, but at typical board sizes that cost is negligible.

## Considered Options: concurrent creation conflicts

### Pessimistic locking
Wrap the read, shift, and insert in one transaction with strong isolation, so a second concurrent create queues and succeeds instead of failing.
Nearly free on SQLite today, since it already serializes all writes at the whole-database level, but that changes once we move to Postgres or SQL Server: locking there scopes to the contended rows, an improvement, but members creating columns on the same board still block and wait.
It turns a retryable failure into a longer request rather than removing the wait, and needs a busy timeout configured (none exists today).

### Optimistic concurrency, single attempt
Rely on the unique `(BoardId, Position)` index to detect a collision; on failure, return 409 immediately.
Simplest option, and it never blocks a request behind another member's write, but it surfaces every collision to the caller, and no client here implements retry logic yet.

### Optimistic concurrency, bounded retry
Same detection, but `CreateAsync` retries the read, normalize, shift, insert cycle internally (bounded attempts) before returning 409.
Each retry re-reads state, so a collision on one attempt resolves on the next.
Keeps the concurrency concern inside `ColumnService` instead of pushing it onto every client, at the cost of a retry loop and continued coupling to the database's constraint-violation exception shape.

## Decision
We will normalize and shift positions on create, and resolve conflicts with bounded, server-side optimistic retries.

Normalize & shift keeps `Position` a dense `1..N` ordinal, matching `BoardService`'s existing ordering, and means callers never need to know a board's current column count to get a sensible result.

We chose optimistic concurrency over pessimistic locking because the app is expected to move off SQLite.
Locking is nearly free on SQLite only because it already serializes all writes at the whole-database level; on a real provider, an equivalent guarantee still makes concurrent editors of the same board wait, just as a queued request instead of a retryable one.
Optimistic retry keeps that cost local to the contended operation, and resolves within a couple of attempts given how small and fast each write is.
409 becomes a rare, sustained-contention signal rather than a common failure mode.