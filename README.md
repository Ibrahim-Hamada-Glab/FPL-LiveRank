# FPL Live Rank

Live Fantasy Premier League scoring and mini-league rank calculations, built on the public FPL API.

## Status

**Phase 4 complete.**

Backend: manager live scoring now includes captaincy projection, formation-aware auto-subs, Bench Boost short-circuiting, and exact on-demand mini-league live rank. `LeagueLiveRankService` fetches paginated public FPL classic-league standings, calculates every member through the same manager live pipeline, then ranks by live season total with tie flags and rank movement.

Frontend: Angular 19 + Tailwind has a manager live page and a `/league/:id` mini-league table with search, sorting, rank movement, captain/chip columns, and links back to manager details.

Not yet implemented: SignalR push / Hangfire jobs / Redis snapshot caching (Phase 5), effective ownership and rank-change explanations (Phase 6).

## Tech stack

- .NET 10 Web API (Clean Architecture: Api / Application / Domain / Infrastructure)
- EF Core 10 + Npgsql
- StackExchange.Redis (with `NullCacheService` fallback when disabled)
- Polly (retry on 5xx/timeout/429, exponential backoff)
- Serilog console sink
- OpenAPI at `/openapi/v1.json`
- xUnit + FluentAssertions for tests

## Running locally

### Prerequisites

- .NET 10 SDK
- Docker (for Postgres + Redis), or local Postgres 16+ and Redis 7+

### With docker-compose

```bash
docker compose up --build
# API:      http://localhost:8080
# OpenAPI:  http://localhost:8080/openapi/v1.json
# Health:   http://localhost:8080/health
```

### Without Docker

```bash
# 1. start Postgres + Redis however you prefer (matching connection strings in appsettings.json)
# 2. restore + run
dotnet restore
dotnet run --project src/FplLiveRank.Api
```

To run without Redis, set `Redis__Enabled=false` (or `Redis:Enabled` in JSON). Cache calls become no-ops and every request hits the FPL API directly.

### Frontend (Angular 19)

```bash
cd client
npm install      # only first time
npm start        # http://localhost:4200
```

`ng serve` reads [client/proxy.conf.json](client/proxy.conf.json) and forwards `/api/*` to `http://localhost:8080`, so run the .NET API alongside. CORS in the API also allows `http://localhost:4200` directly if you prefer to skip the proxy.

Production build: `npm run build` (output at `client/dist/client`).

## API endpoints

| Method | Path                                  | Description                                                                 |
|-------:|---------------------------------------|-----------------------------------------------------------------------------|
| GET    | `/api/fpl/events/current`             | Resolves current (or upcoming) gameweek from `bootstrap-static`.            |
| POST   | `/api/fpl/bootstrap/sync`             | Forces a refresh of cached bootstrap data.                                  |
| GET    | `/api/fpl/manager/{managerId}/live`   | Live GW points, season total, pick-by-pick breakdown. `?eventId=` optional. |
| GET    | `/api/fpl/league/{leagueId}/live`     | Exact live mini-league rank table. `?eventId=` optional.                    |
| GET    | `/health`                             | Liveness probe.                                                             |

### Sample request

```bash
curl http://localhost:8080/api/fpl/manager/12345/live | jq .
```

Response (abbreviated):

```json
{
  "managerId": 12345,
  "eventId": 30,
  "rawLivePoints": 64,
  "transferCost": 0,
  "livePointsAfterHits": 64,
  "previousTotal": 1820,
  "liveSeasonTotal": 1884,
  "activeChip": "None",
  "captainElementId": 351,
  "viceCaptainElementId": 182,
  "picks": [
    { "elementId": 351, "webName": "Salah", "position": 11, "multiplier": 2,
      "isCaptain": true, "liveTotalPoints": 12, "minutes": 90, "bonus": 3,
      "contributedPoints": 24 }
  ]
}
```

## Calculation flow

`ManagerLiveScoreService.GetAsync(managerId, eventId?)`:

1. Resolve gameweek via `IFplBootstrapService.GetCurrentEventAsync` if not supplied.
2. Parallel fetch (cached in Redis): `picks`, `event/{id}/live`, `fixtures`, `entry/{id}/history`, bootstrap players.
3. Apply `CaptaincyProjector`, then `AutoSubProjector`, then `LivePointsCalculator`:
   - `rawLivePoints = Σ(player.live_total_points × pick.multiplier)`
   - `livePointsAfterHits = rawLivePoints − transferCost`
4. Look up `previousTotal` from history (highest event < current).
5. `liveSeasonTotal = previousTotal + livePointsAfterHits`.

`LeagueLiveRankService.GetAsync(leagueId, eventId?)` fetches all league standings pages, calculates each manager with bounded concurrency, sorts by live season total, and returns live rank plus `officialRank - liveRank` movement.

## Caching

| Key                              | TTL    |
|----------------------------------|--------|
| `bootstrap`                      | 30 min |
| `event:{id}:live`                | 45 s   |
| `event:{id}:fixtures`            | 45 s   |
| `manager:{id}:event:{e}:picks`   | 2 min  |
| `manager:{id}:history`           | 10 min |
| `league:{id}:standings:page:{n}` | 2 min  |

Keys are prefixed with `Redis:KeyPrefix` (default `fpl:`).

## Tests

```bash
dotnet test
```

Coverage so far (50 unit + 6 integration tests):

- `LivePointsCalculator` — basic sum, captain ×2 / ×3, bench multiplier=0, transfer hit deduction, missing live stat → 0, negative cost guard.
- `FplApiClient` — JSON parsing for bootstrap and event-live, 404/5xx mapping to `FplApiException`, `?event=` query param.
- `CaptaincyProjector` — captain-played no-op, blanked-captain + finished team promotes vice, blanked + unfinished team stays Projected, Triple Captain carries multiplier 3 through the swap, both blanked yields NoCaptainPoints, vice yet-to-play stays Projected.
- `AutoSubProjector` — no-blanks no-op, outfield blank → first eligible bench (in priority order), GK only swaps with bench GK, blocked-by-formation paths, bench candidate already used not reused, unfinished-team blank flags projection pending.
- `LeagueLiveRankService` — paginated standings, cache keys, bounded manager scoring path, live ranking, tie flags, rank movement, empty/invalid league errors.

## Project layout

```
src/
  FplLiveRank.Api/              # controllers, middleware, DI host
  FplLiveRank.Application/      # services, calculators (pure), DTOs, FPL response models, interfaces
  FplLiveRank.Domain/           # entities, enums, value records
  FplLiveRank.Infrastructure/   # EF DbContext, FPL HttpClient, Redis cache, Polly policies
tests/
  FplLiveRank.UnitTests/
  FplLiveRank.IntegrationTests/
client/                         # Angular 19 + Tailwind frontend
  src/app/api/                  # manager/league API services + DTO types
  src/app/pages/manager-live/
  src/app/pages/league-live/
```

## Known limitations

- No EF migrations created yet — DbContext is wired but unused at runtime. Phase 4 computes on demand; Phase 5 persistence/snapshots will need migrations.
- No SignalR / Hangfire refresh jobs yet.
- `manager.PlayerName` / `TeamName` returned as empty strings until `GET /entry/{id}/` is wired (Phase 2).

## Legal / ethics

Uses public FPL endpoints only. Caches aggressively, retries with exponential backoff to avoid hammering the upstream. No paid-product branding, endpoints, or proprietary algorithms are reused.
