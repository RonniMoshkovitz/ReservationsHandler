# ReservationsHandler

ReservationsHandler is a service that sits in front of a reservations table and handles traffic and duplicate data. It also provides per-supplier stats (ingested vs. throttled).

## Running & endpoints

```bash
dotnet run --project ReservationsHandler.Api     # Swagger UI at /swagger in Development
dotnet test
```

- `POST /api/reservations/ingest` returns `201` when the reservation is new, and `200` with `status` = `Updated`, `Duplicate` (nothing written) or `Stale` (nothing written), so the response makes clear whether anything changed. Invalid input gets a `400` problem+json, and a throttled supplier gets a `429` with a `Retry-After` header (whole seconds).
- `GET /api/reservations/stats/{supplierId}` returns `{ supplierId, ingested, throttled }`. An unknown supplier gets zeros.

## Assumptions

- **Identity is `(supplierId, reservationId)`:** If a reservation's identity were only its `reservationId`, one supplier could overwrite another's record. This seemed off to me, so I assumed that suppliers issue `reservationId`s independently of each other.
- **"Same details":** I decided that two requests have the same details when room, check-in, check-out, price and `updatedAtUtc` are all equal. That is a `Duplicate`, and nothing is written. I included `updatedAtUtc` in the comparison because I update the reservation based on it: an older `updatedAtUtc` than the stored one is `Stale` (no write), so a delayed retry can't overwrite newer data. For the same reason, identical data with a newer timestamp is not a `Duplicate`. Otherwise the stored `updatedAtUtc` would fall behind, and an update with a timestamp between the original and the newer one could overwrite it. An equal `updatedAtUtc` with different details is treated as an update.
- **Throttle:** Ingestion requests are rate limited per supplier as the assignment states, using a sliding window log algorithm. Only accepted requests fill the window, so rejected ones don't extend it. Duplicates and stale updates still count (the spec limits requests *sent*). Rejected requests increment the persisted throttled counter.
- **Concurrency:** each ingest reads, decides and writes inside one serializable transaction (SQLite's `BEGIN IMMEDIATE`), so two identical requests arriving together produce exactly one `Created`. The cost is that duplicate and stale retries also wait for the write lock, which I accepted for simplicity.
- **Ingestion processing order:** I went with this order: validate, then throttle, then ingest. This is because the limiter keys on `supplierId`, which an invalid body may lack, and the throttle must come before any reservations-table access. Cost: a flood of *invalid* requests isn't throttled.
- **Ingested calculation:** `ingested = created + updated`. Duplicates, stale updates and throttled requests are not counted as ingested, as they are ignored or throttled.
- **Reservation validations:** Ids are trimmed and case-sensitive. `checkOut` must be strictly after `checkIn`, and `price >= 0`.
- **Explicit timestamp offsets:** timestamp values are expected to carry an offset (`Z` or `+02:00`). One without an offset is read as *server-local* time.
- **Known limits:** the limiter is in memory (lost on restart, not shared across instances), so production needs external storage such as Redis. SQLite allows one writer, so under a flood the small throttled-counter write can queue behind others.

## Claude Code suggestions I disagreed with or changed

Before a single line of code was written, I made a thorough plan with Claude Code using plan mode. We first declared the scope of the project, defined behavior and planned each step of the implementation. Only then did I let Claude work on the project with me, step by step. This made the work cleaner and let me have fewer disagreements with Claude, as we had set a plan and made decisions up front, so we were mostly aligned.

- **SQLite mode:** Claude suggested switching SQLite to WAL mode with `synchronous=NORMAL` (instead of the default `FULL`) for faster commits. I disagreed because it can cause the last few commits to be lost after a power failure, and I preferred strong consistency.
- **Tests scope:** I forgot to scope the testing step, which resulted in Claude writing over 200 tests, not including planned integration tests. I decided it was way over the assignment's scope and made Claude reduce the tests to a small, precise unit test suite (41 tests) that matches the assignment's expectation of "enough tests to give us confidence the dedup and throttle logic actually work".
- **Offset-less timestamp converter:** Claude built a converter that rejected timestamps without an explicit offset. It worked, but it added a lot of extra code for a very specific edge case that the spec's examples don't hit. As it was a negligible case compared to the assignment's requirements and logic, and it required time and extra code, I decided to focus on the assignment requirements first and document the limitation (timestamps with no explicit offset are treated as local time).
- **Over-layering removed:** Claude implemented many redundant extra classes that split up logic that belongs together (for example, a separate static class for `Retry-After` rounding that held a single one-line function). I made sure to fix these.
- **Coupling:** I pushed back on implementations where Claude coupled logic to a specific usage or specific instances when it should have depended on abstractions (for example, `ISupplierRateLimiter` is now a generic `IRateLimiter.TryAcquire(id)` keyed by whatever string the caller passes). In the data layer, Claude initially coupled everything to SQLite. I changed the implementation to use an `IDbConnectionFactory` that returns a plain `DbConnection`, implemented by `SqliteConnectionFactory`. The SQL text itself (`ON CONFLICT`) is still SQLite-flavored, so swapping the database is cleaner but not free (with more time I'd address it better).

## With more time

- Add more tests, including integration tests for the SQL write path and the HTTP contract, including the parallel-duplicates case.
- Add a distributed limiter (using Redis) and eviction of idle limiter windows.
- Choose a different storage (without SQLite's single writer).
- Reject offset-less timestamps with a 400.
- Add idempotency keys, auth, and metrics.
