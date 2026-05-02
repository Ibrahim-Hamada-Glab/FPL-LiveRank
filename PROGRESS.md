# FPL Live Rank — progress & handoff

> Reading this cold? You're picking up an in-progress build of an FPL live-scoring app. The phases below come from `FplLive Plan.txt §22`. The README is the *user-facing* doc; this file is the *next-agent* doc.

## Phase status


| Phase | Scope                                                                                                                              | Status |
| ----- | ---------------------------------------------------------------------------------------------------------------------------------- | ------ |
| 1     | Solution scaffold, FPL API client (Polly), Redis cache, EF Core context, current-event detection, `GET /api/fpl/manager/{id}/live` | ✅      |
| 2     | Captaincy projection (vice promotion when captain blanks + team finished) + Angular 19 + Tailwind frontend page                    | ✅      |
| 3     | Auto-sub projector (formation-aware, single-use bench), Bench Boost short-circuit, Auto-subs UI panel                              | ✅      |
| 4     | League standings import, exact live mini-league rank, Angular league table                                                         | ✅      |
| 5     | SignalR push, Hangfire jobs, Redis snapshot caching                                                                                | ✅      |
| 6     | Effective ownership, "why my rank changed" explanations                                                                            | ✅      |
| 7     | Test/Docker/README polish (migrations, Swagger, Docker, hardening, frontend smoke)                                                 | ✅      |


Tests: **70 unit + 9 integration, all passing** (last run 2026-05-02). Angular production build and spec type-check also pass; local Karma runtime is blocked until Chrome/Chromium is installed or `CHROME_BIN` is set.

## Architecture invariants (non-obvious)

1. **Calculation pipeline order** in `ManagerLiveScoreService.GetAsync`:
  `picks → CaptaincyProjector → AutoSubProjector → LivePointsCalculator`. Don't reorder. Captaincy must run first because vice-promotion depends on official captain status; auto-sub runs after because the bench player coming on does NOT inherit the captain multiplier (FPL rule: if both captain and vice blank, no double points).
2. **Calculators are pure functions.** No IO, no DI, no logger. Tests construct dictionaries directly. Keep it that way.
3. **`TreatWarningsAsErrors=true`** on every `src/` project. NU1903 (vulnerable transitive package) was hit during Phase 1 — pinned `System.Security.Cryptography.Xml` to `10.0.7` in `Infrastructure.csproj` to override the vulnerable transitive from EF Core. If you bump EF or Npgsql, re-check this pin.
4. **Bench Boost** skips auto-sub entirely (FPL already counts all 15 with multiplier=1). The check is in `ManagerLiveScoreService` — `if (activeChip != ChipType.BenchBoost)`.
5. `**finished_provisional`** counts as "finished" for captaincy/auto-sub decisions. FPL only flips `finished` after bonus is awarded, which is too late for our purposes.
6. **Cache contract** (`ICacheService`): `T : class`. Fixtures had to be cached as `List<FplFixture>` rather than `IReadOnlyList<...>` because of JSON deserialization on cache hit.
7. **Layering**: `Application` cannot reference `Infrastructure`. FPL response DTOs live in `Application/External/Fpl/Models/`, not `Infrastructure/`. Don't move them back.
8. **xUnit `[Fact]`** is resolved via `tests/FplLiveRank.UnitTests/GlobalUsings.cs` (`global using Xunit;`). The integration tests file uses an explicit `using Xunit;` — leave it; don't add a global using there too.
9. **Snapshot caching is read-through**: `ManagerLiveScoreService.GetAsync` and `LeagueLiveRankService.GetAsync` cache the *computed DTO* (not just the upstream FPL responses) for ~30s, with a Redis `SET NX PX` lock so concurrent cache-miss callers wait on a single recompute instead of stampeding. The lock implementation in `RedisCacheService` uses a Lua compare-and-delete script — don't replace that with a plain `DEL` or you'll race lock holders. `NullCacheService.AcquireLockAsync` always returns a no-op handle, so unit tests with no Redis behave as if every caller wins the lock.
10. `**IFplLiveBroadcaster` has two implementations**: `NullFplLiveBroadcaster` (registered by `AddApplication`) and `SignalRFplLiveBroadcaster` (registered by Api Program.cs, replacing the null one via `RemoveAll<IFplLiveBroadcaster>()`). This keeps `Application` testable without the SignalR runtime. SignalR group naming is centralised in `FplLiveHub.{Manager,League,Event}Group(...)` — the broadcaster reuses those helpers, keep them in sync.
11. **Hangfire uses PostgreSQL storage** (`Hangfire.PostgreSql`) against `ConnectionStrings:Postgres`, so recurring jobs survive restarts and are safe for multi-instance API deployments. Hangfire creates its own tables automatically. The app EF tables are managed by `InitialCreate`.
12. **Newtonsoft.Json is pinned to 13.0.3** in `FplLiveRank.Api.csproj` as a defensive override for older Hangfire transitive restores. If you bump Hangfire and that transitive resolves to a safe version, the pin can be removed — but verify with `dotnet list package --vulnerable --include-transitive`.

## File map for future work

### Backend calculators (pure)

- `src/FplLiveRank.Application/Calculators/LivePointsCalculator.cs` — Σ(points × multiplier) − transferCost.
- `src/FplLiveRank.Application/Calculators/CaptaincyProjector.cs` — vice promotion logic.
- `src/FplLiveRank.Application/Calculators/AutoSubProjector.cs` — formation-validating sub logic.

### Backend orchestrators

- `src/FplLiveRank.Application/Services/ManagerLiveScoreService.cs` — the only place these three calculators are wired together. Reads through the manager-live snapshot cache (`SnapshotCache.GetOrComputeAsync`) so concurrent viewers reuse the result.
- `src/FplLiveRank.Application/Services/ManagerLeaguesService.cs` — maps `GET /entry/{managerId}/` into discoverable classic leagues for the UI and caches the entry profile for 10 min.
- `src/FplLiveRank.Application/Services/LeagueLiveRankService.cs` — pulls paginated classic-league standings, calls `IManagerLiveScoreService.GetAsync` for each member with bounded concurrency, then sorts live ranks. `RefreshAsync` recomputes under the league refresh lock and broadcasts `LeagueLiveTableUpdated` + `RefreshProgressUpdated` events.
- `src/FplLiveRank.Application/Services/SnapshotCache.cs` — read-through helper that wraps cache-get + lock-acquire + double-check + compute. Used by both manager and league services. If the lock-holder takes longer than the 8-second poll budget, the helper falls through to compute without lock (rare, but keeps requests live during Redis blips).
- `src/FplLiveRank.Application/Services/NullFplLiveBroadcaster.cs` — default `IFplLiveBroadcaster` registered by `AddApplication`; replaced by `SignalRFplLiveBroadcaster` in the Api project.

### Background jobs

- `src/FplLiveRank.Application/Jobs/EventLiveRefreshJob.cs` — Hangfire recurring job that re-fetches event-status, event-live, and fixtures every minute, then broadcasts `EventLiveRefreshed`. The recurring schedule is wired in `Program.cs` via `IRecurringJobManager.AddOrUpdate`.

### SignalR

- `src/FplLiveRank.Api/Hubs/FplLiveHub.cs` — hub at `/hubs/fpl-live`. Clients call `JoinManager(id)` / `JoinLeague(id)` / `JoinEvent(id)` (and `Leave...`) to subscribe to update streams.
- `src/FplLiveRank.Api/Hubs/SignalRFplLiveBroadcaster.cs` — `IFplLiveBroadcaster` impl that fans out to the relevant group. Server-emitted client method names: `ManagerLiveScoreUpdated`, `LeagueLiveTableUpdated`, `EventLiveRefreshed`, `RefreshProgressUpdated`. Mirror these exactly when wiring the Angular SignalR client.

### Backend DTO

- `src/FplLiveRank.Application/DTOs/ManagerLiveDto.cs` — every new field surfaced by future calculators goes here. Currently 18 fields + `Picks` list + `SubstitutionDto`. **If you grow this much further, split into nested objects** (`CaptaincyInfo`, `AutoSubInfo`).

### Frontend

- `client/src/app/api/manager-live.types.ts` — TS types **mirror** the C# DTO. Whenever you add a field server-side, update here.
- `client/src/app/api/manager-live.service.ts` — uses `environment.apiBaseUrl` and now exposes both live score and manager-leagues calls.
- `client/src/app/api/league-live.types.ts` / `league-live.service.ts` — TS contract/client for `GET /api/fpl/league/{id}/live`.
- `client/src/app/pages/manager-live/manager-live.component.{ts,html}` — signals-based page, saves manager ID in localStorage, loads discoverable mini-leagues, and links to league live tables.
- `client/src/app/pages/league-live/league-live.component.{ts,html}` — sortable/searchable mini-league table.
- Routing is set up at `client/src/app/app.routes.ts` with lazy routes for `/` and `/league/:id`.

### Tests

- `tests/FplLiveRank.UnitTests/Calculators/*.cs` — three test files matching the three calculators.
- `tests/FplLiveRank.UnitTests/Services/ManagerLeaguesServiceTests.cs` — manager entry mapping/cache/validation.
- `tests/FplLiveRank.UnitTests/Services/LeagueLiveRankServiceTests.cs` — pagination, cache keys, ranking/tie movement, invalid/empty league paths.
- `tests/FplLiveRank.UnitTests/Services/SnapshotCachingTests.cs` — Phase 5 snapshot read-through, manual refresh broadcast, and lock-held-elsewhere fallback paths.
- `tests/FplLiveRank.UnitTests/Jobs/EventLiveRefreshJobTests.cs` — Hangfire job warms the right cache keys and swallows FPL errors.
- `tests/FplLiveRank.UnitTests/Support/InMemoryCacheService.cs` — Redis-shaped fake (JSON store + atomic NX lock) for Phase 5+ tests. Use it whenever you need real cache semantics; `RecordingCacheService` is still the go-to for "did we ask for this key" assertions.
- `tests/FplLiveRank.IntegrationTests/ApiSmokeTests.cs` — `WebApplicationFactory`-driven; uses fakes for `IManagerLiveScoreService` etc. **When you change the DTO**, update the fake response in this file too — the build error is loud but easy to miss in a hurry. The `FakeLeagueLiveRankService` exposes a `RefreshCalls` counter for the manual-refresh endpoint test.

## Recent user-side changes (made between Phase 3 and now)

The user added explicit environment files for the Angular client; Phase 4 normalized both to relative `/api`:

- `client/src/environments/environment.ts` — dev (`apiBaseUrl: '/api'`)
- `client/src/environments/environment.prod.ts` — prod (`apiBaseUrl: '/api'`)
- `client/angular.json` — `fileReplacements` block for production config to swap dev → prod env file
- `client/src/app/api/manager-live.service.ts` — now calls `${environment.apiBaseUrl}/fpl/manager/{id}/live` instead of the hard-coded `/api/...`

## Phase 4 — implemented

Plan section 6 covers FPL league endpoint:
`GET /api/leagues-classic/{leagueId}/standings/?page_standings={n}` (paginated, 50 per page).

- `IFplApiClient.GetLeagueStandingsAsync(leagueId, page, ct)` implemented in `FplApiClient`.
- FPL model: `Application/External/Fpl/Models/LeagueStandingsResponse.cs`.
- Cache key: `league:{id}:standings:page:{n}`, TTL 2 min.
- `ILeagueLiveRankService` / `LeagueLiveRankService` implemented in Application.
- Endpoint: `GET /api/fpl/league/{leagueId}/live?eventId=`.
- Angular route: `/league/:id`, sortable/searchable table, manager row links back to `/?managerId=...&eventId=...`.
- Follow-up UX slice: `GET /api/fpl/manager/{managerId}/leagues`, manager ID localStorage persistence, editable saved manager, and mini-league selection without manually typing a league ID.

**Performance note**: a 50-manager league = 50 calls to `ManagerLiveScoreService.GetAsync`, each of which fans out to 5 FPL endpoints. Even with caching, the first request after a cold cache could hit FPL ~250 times. Consider caching the result of `ManagerLiveScoreService` itself behind a key like `manager:{id}:event:{e}:live` for ~30s during live GWs.

## Phase 5 — implemented

1. **Snapshot cache** for both `ManagerLiveScoreService` and `LeagueLiveRankService` results: keys `manager:{id}:event:{e}:live` and `league:{id}:event:{e}:live`, TTL 30s. Implemented via `SnapshotCache.GetOrComputeAsync` (read-through with double-check inside the lock).
2. **Distributed lock** added to `ICacheService.AcquireLockAsync` — Redis-backed via `SET NX PX` and Lua compare-and-delete on release. `NullCacheService` always grants the lock, so unit tests don't need Redis.
3. **SignalR hub** `/hubs/fpl-live` with `manager-{id}`, `league-{id}`, `event-{id}` groups. `IFplLiveBroadcaster` lives in Application; `SignalRFplLiveBroadcaster` (in Api) replaces the default `NullFplLiveBroadcaster` at startup.
4. **Hangfire** wired in Api with memory storage. One recurring job: `EventLiveRefreshJob` (every minute) re-warms event-status/event-live/fixtures and broadcasts `EventLiveRefreshed`.
5. **Manual refresh endpoint** `POST /api/fpl/league/{leagueId}/refresh?eventId={n}` — invokes `LeagueLiveRankService.RefreshAsync`, which takes the per-league refresh lock, recomputes, broadcasts the new table, and returns the fresh DTO. If the lock is already held the endpoint returns the existing snapshot without recomputing (and broadcasts a `skipped` progress event).

## Phase 6 — implemented

1. **Pure EO calculator** `EffectiveOwnershipCalculator` (`Application/Calculators/`) emitting per-player Ownership / Captaincy / EO / RankImpactPerPoint plus a friendly `ImpactExplanation` string.
2. **`ILeagueEffectiveOwnershipService` + `LeagueEffectiveOwnershipService`** reuse `ILeagueLiveRankService.GetAsync` so standings + per-manager picks come from existing snapshot caches; results land in their own `league:{id}:event:{e}:eo` snapshot for ~30s. Bounded concurrency (`MaxConcurrentManagerLoads = 8`); per-manager fetch failures are logged + skipped.
3. **`GET /api/fpl/league/{leagueId}/effective-ownership`** on `LeagueController` (optional `eventId` and `managerId`).
4. **Rank-change explanations.** `LeagueLiveRankService.RefreshAsync` writes the prior snapshot to `league:{id}:event:{e}:live:prev` (TTL 6h). `LeagueLiveRankEntryDto` exposes `PreviousLiveRank`, `RankDeltaSincePreviousSnapshot`, and a human-readable `RankChangeExplanation`. League-live UI renders the delta inline under the Move column.
5. **Angular `/league/:id/eo` page** (`pages/league-effective-ownership/`) — sortable EO table, search, optional Manager ID for personalised rank impact, and a back link to the live league table.

## Phase 7 — implemented

1. **Swagger UI** — `Swashbuckle.AspNetCore` 10.1.7 wired via `AddSwaggerGen()` and exposed at `/swagger` in Development alongside `/openapi/v1.json`.
2. **`Retry-After` honoring** — Polly policy moved to `FplLiveRank.Infrastructure.External.Fpl.FplRetryPolicies` so it can be unit-tested without DI. Reads delta-seconds and HTTP-date `Retry-After` headers, falls back to exponential backoff, capped at 30 s. Tests in `Infrastructure/RetryAfterPolicyTests.cs`.
3. **Angular global `ErrorHandler`** — `client/src/app/core/global-error-handler.ts` registered in `app.config.ts`. Logs to console and renders a dismissible banner so uncaught render errors no longer white-screen the SPA.
4. **Client Docker image** — `client/Dockerfile` builds the Angular dist on `node:22-alpine`, then serves on `nginx:1.27-alpine` port 8081 with `client/nginx.conf` reverse-proxying `/api` and `/hubs` to the `api` container. `docker-compose.yml` adds a `web` service and an API healthcheck; CORS allowlist includes `http://localhost:8081`.
5. **EO smoke test** — `League_effective_ownership_endpoint_returns_eo_table` in `ApiSmokeTests` exercises the controller through the test host with a fake EO service.
6. **EF Core InitialCreate migration** — migration added under `Infrastructure/Persistence/Migrations`. Current decision: live views remain compute-on-demand + Redis snapshot cached; EF tables are ready for later write-behind persistence, but no app path writes live snapshots yet.
7. **Hangfire PostgreSQL storage** — `Hangfire.MemoryStorage` replaced by `Hangfire.PostgreSql` using the same Postgres connection string as `AppDbContext`.
8. **Manager name enrichment** — `ManagerLiveScoreService` now populates `PlayerName` and `TeamName` from the cached `manager:{id}:entry` response, removing the empty-string DTO fields.
9. **Frontend smoke coverage** — Karma specs render the live league and EO standalone pages against fake API responses. `client/package.json` exposes `npm run test:ci`; local execution needs Chrome/Chromium or `CHROME_BIN`.
10. **README polish** — covers env vars, backend/frontend/docker run paths, migration command, calculation details, screenshots placeholder, TRX + coverage test command, and current limitations.

## How to run + verify

```bash
# backend
dotnet restore FplLiveRank.slnx
dotnet build FplLiveRank.slnx
dotnet test FplLiveRank.slnx              # 70 unit + 9 integration
dotnet test FplLiveRank.slnx --logger "trx;LogFileName=fpl-live-rank.trx" --collect:"XPlat Code Coverage" --results-directory TestResults

# frontend
cd client
npm install                                # only first time
npm run build                              # or `npm start` for dev server on 4200
npm run test:ci                            # ChromeHeadless + coverage

# everything together (Postgres + Redis + API + Web)
docker compose up --build                  # API on :8080, Web on :8081
# Swagger UI: http://localhost:8080/swagger (Development env)
```

## Known limitations / still to do

- **No write-behind persistence yet.** `InitialCreate` exists, but live manager/league views are still computed on demand and cached in Redis. Persisting `LiveManagerScore` / `LivePlayerPoint` snapshots can be a later durability/reporting phase.
- **Cold large leagues can still fan out.** Manager live snapshots, entry profiles, and league pages are cached, but the first cold request for a large league still performs many public FPL requests.

## Sandbox-specific gotchas observed this session

- `dotnet new` hangs the first time (NuGet metadata download). Workaround used: hand-write `.csproj` files. Avoid `dotnet new` if NuGet hasn't been warmed.
- Angular CLI scaffold via `npx --yes @angular/cli@19 new` takes ~3 min cold (downloads CLI + workspace `npm install`). Don't poll — use Monitor.

