# FPL Live Rank — progress & handoff

> Reading this cold? You're picking up an in-progress build of an FPL live-scoring app. The phases below come from `FplLive Plan.txt §22`. The README is the *user-facing* doc; this file is the *next-agent* doc.

## Phase status

| Phase | Scope | Status |
|------:|-------|:------:|
| 1 | Solution scaffold, FPL API client (Polly), Redis cache, EF Core context, current-event detection, `GET /api/fpl/manager/{id}/live` | ✅ |
| 2 | Captaincy projection (vice promotion when captain blanks + team finished) + Angular 19 + Tailwind frontend page | ✅ |
| 3 | Auto-sub projector (formation-aware, single-use bench), Bench Boost short-circuit, Auto-subs UI panel | ✅ |
| 4 | League standings import, exact live mini-league rank, Angular league table | ✅ |
| 5 | SignalR push, Hangfire jobs, Redis snapshot caching | ⏳ next |
| 6 | Effective ownership, "why my rank changed" explanations | — |
| 7 | Test/Docker/README polish | — |

Tests: **50 unit + 6 integration, all passing** (last run 2026-04-28). Angular production build also passes.

## Architecture invariants (non-obvious)

1. **Calculation pipeline order** in `ManagerLiveScoreService.GetAsync`:
   `picks → CaptaincyProjector → AutoSubProjector → LivePointsCalculator`. Don't reorder. Captaincy must run first because vice-promotion depends on official captain status; auto-sub runs after because the bench player coming on does NOT inherit the captain multiplier (FPL rule: if both captain and vice blank, no double points).
2. **Calculators are pure functions.** No IO, no DI, no logger. Tests construct dictionaries directly. Keep it that way.
3. **`TreatWarningsAsErrors=true`** on every `src/` project. NU1903 (vulnerable transitive package) was hit during Phase 1 — pinned `System.Security.Cryptography.Xml` to `10.0.7` in `Infrastructure.csproj` to override the vulnerable transitive from EF Core. If you bump EF or Npgsql, re-check this pin.
4. **Bench Boost** skips auto-sub entirely (FPL already counts all 15 with multiplier=1). The check is in `ManagerLiveScoreService` — `if (activeChip != ChipType.BenchBoost)`.
5. **`finished_provisional`** counts as "finished" for captaincy/auto-sub decisions. FPL only flips `finished` after bonus is awarded, which is too late for our purposes.
6. **Cache contract** (`ICacheService`): `T : class`. Fixtures had to be cached as `List<FplFixture>` rather than `IReadOnlyList<...>` because of JSON deserialization on cache hit.
7. **Layering**: `Application` cannot reference `Infrastructure`. FPL response DTOs live in `Application/External/Fpl/Models/`, not `Infrastructure/`. Don't move them back.
8. **xUnit `[Fact]`** is resolved via `tests/FplLiveRank.UnitTests/GlobalUsings.cs` (`global using Xunit;`). The integration tests file uses an explicit `using Xunit;` — leave it; don't add a global using there too.

## File map for future work

### Backend calculators (pure)
- `src/FplLiveRank.Application/Calculators/LivePointsCalculator.cs` — Σ(points × multiplier) − transferCost.
- `src/FplLiveRank.Application/Calculators/CaptaincyProjector.cs` — vice promotion logic.
- `src/FplLiveRank.Application/Calculators/AutoSubProjector.cs` — formation-validating sub logic.

### Backend orchestrators
- `src/FplLiveRank.Application/Services/ManagerLiveScoreService.cs` — the only place these three calculators are wired together.
- `src/FplLiveRank.Application/Services/LeagueLiveRankService.cs` — pulls paginated classic-league standings, calls `IManagerLiveScoreService.GetAsync` for each member with bounded concurrency, then sorts live ranks.

### Backend DTO
- `src/FplLiveRank.Application/DTOs/ManagerLiveDto.cs` — every new field surfaced by future calculators goes here. Currently 18 fields + `Picks` list + `SubstitutionDto`. **If you grow this much further, split into nested objects** (`CaptaincyInfo`, `AutoSubInfo`).

### Frontend
- `client/src/app/api/manager-live.types.ts` — TS types **mirror** the C# DTO. Whenever you add a field server-side, update here.
- `client/src/app/api/manager-live.service.ts` — uses `environment.apiBaseUrl` (user added env files in this session — see "Recent user-side changes" below).
- `client/src/app/api/league-live.types.ts` / `league-live.service.ts` — TS contract/client for `GET /api/fpl/league/{id}/live`.
- `client/src/app/pages/manager-live/manager-live.component.{ts,html}` — single page, signals-based, Tailwind.
- `client/src/app/pages/league-live/league-live.component.{ts,html}` — sortable/searchable mini-league table.
- Routing is set up at `client/src/app/app.routes.ts` with lazy routes for `/` and `/league/:id`.

### Tests
- `tests/FplLiveRank.UnitTests/Calculators/*.cs` — three test files matching the three calculators.
- `tests/FplLiveRank.UnitTests/Services/LeagueLiveRankServiceTests.cs` — pagination, cache keys, ranking/tie movement, invalid/empty league paths.
- `tests/FplLiveRank.IntegrationTests/ApiSmokeTests.cs` — `WebApplicationFactory`-driven; uses fakes for `IManagerLiveScoreService` etc. **When you change the DTO**, update the fake response in this file too — the build error is loud but easy to miss in a hurry.

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

**Performance note**: a 50-manager league = 50 calls to `ManagerLiveScoreService.GetAsync`, each of which fans out to 5 FPL endpoints. Even with caching, the first request after a cold cache could hit FPL ~250 times. Consider caching the result of `ManagerLiveScoreService` itself behind a key like `manager:{id}:event:{e}:live` for ~30s during live GWs.

## Phase 5 — concrete starting points

1. Add a short-lived cached league live-table snapshot (`fpl:league:{id}:live:{eventId}`) so repeated viewers don't recalculate every manager.
2. Add SignalR hub `/hubs/fpl-live` with `league-{id}`, `manager-{id}`, and `event-{id}` groups.
3. Add a background refresh job (Hangfire or Quartz) to refresh event live/fixtures/status during active matches.
4. Add distributed locking for per-league refreshes before adding manual refresh endpoints.

## How to run + verify

```bash
# backend
dotnet restore FplLiveRank.slnx
dotnet build FplLiveRank.slnx
dotnet test FplLiveRank.slnx              # 50 unit + 6 integration

# frontend
cd client
npm install                                # only first time
npm run build                              # or `npm start` for dev server on 4200

# everything together (Postgres + Redis + API)
docker compose up --build
```

## Known limitations / still to do

- **No EF migrations created.** `AppDbContext` is wired but never `EnsureCreated`'d or migrated. Phase 4 stayed compute-on-demand; Phase 5 snapshot persistence is when this bites.
- **`PlayerName` / `TeamName` on the DTO are empty strings.** `GET /entry/{id}/` is not wired yet (was on the Phase 2 list, slipped). Cheap to add when needed.
- **No retry on FPL 429.** Polly policy retries 5xx + timeout, doesn't yet honor `Retry-After`. Fine for now (cache TTLs are short and FPL doesn't 429 us in practice).
- **Frontend has no error boundary.** Service errors render fine, but uncaught rendering errors will white-screen. Add Angular `ErrorHandler` if you ship.

## Sandbox-specific gotchas observed this session

- `dotnet new` hangs the first time (NuGet metadata download). Workaround used: hand-write `.csproj` files. Avoid `dotnet new` if NuGet hasn't been warmed.
- Angular CLI scaffold via `npx --yes @angular/cli@19 new` takes ~3 min cold (downloads CLI + workspace `npm install`). Don't poll — use Monitor.
