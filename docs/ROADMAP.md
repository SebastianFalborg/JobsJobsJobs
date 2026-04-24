# Roadmap

Forward-looking items that are intentionally out of scope for the current release but should be picked up later. Each item notes the motivating release and the work it replaces or strengthens.

## 1.7.0 — deferred from 1.6.0

### Classify persistence exceptions instead of blanket-swallowing

**Context.** `BackgroundJobRunStore.TryExecuteRead` currently catches every exception from a read-side query, logs it, and returns an empty result. The intent is graceful degradation when the history tables have transient problems (lock timeouts, connection resets, sweep-in-progress), so the dashboard never hard-fails.

The downside surfaced during the 1.6.0 pre-release: the `[Trigger]` reserved-word SQL Server bug and an earlier `Trigger = @1` bug in `GetLatestRuns` both threw deterministic `SqlException` at parse time, got swallowed, and manifested as "History returns 0 runs" / "CRON baseline is always missing" with no visible signal. In 1.6.0 we raised the log level from `Warning` to `Error` so at least operators running Seq / Application Insights / Elmah see the failures, but a true fix is to split the two failure modes.

**Target behaviour.**

- Keep silent-swallow + log for transient / environmental failures:
  - `Microsoft.Data.SqlClient.SqlException` with number in the transient set (`-2`, `4060`, `40197`, `40501`, `40613`, `49918`, `49919`, `49920`, `1205` deadlock, etc.).
  - `OperationCanceledException` from scope / migration activity.
  - `DbException` with provider-reported "connection broken" codes.
- Surface permanent / programmer-bug failures so the dashboard's existing 5xx → Retry-banner path kicks in:
  - SQL syntax errors (SQL Server 102 / 156 class, SQLite "syntax error" messages).
  - Missing table / column errors (SQL Server 208 / 207, SQLite "no such table" / "no such column").
  - `InvalidOperationException` / `NotImplementedException` from the query builder.
  - Any `ArgumentException` bubbling from NPoco parameter binding.

Re-throw the second set as a typed `BackgroundJobRunPersistenceException` that the controller turns into a 500 with a problem-details body; the client already handles 5xx via the consecutive-error counter and the contextual Retry button, so no UI work is needed.

**Acceptance.**

- A unit test plants a deliberate SQL syntax error through a `FakeScopeProvider` and asserts that `QueryRuns` / `GetLatestRuns` re-throw as `BackgroundJobRunPersistenceException`.
- A second unit test plants a transient deadlock (`SqlException` with number `1205`) and asserts the method returns an empty page and logs at `Error`.
- Manual check: on a site with a broken DB, the History tab shows the Retry banner instead of a permanently-empty list.

**Out of scope for 1.7.0.**

- Splitting transient retry (auto-retry with backoff before giving up) is a separate item — the dashboard already has client-side retry so we do not need a server-side retry loop for 1.7.0.
