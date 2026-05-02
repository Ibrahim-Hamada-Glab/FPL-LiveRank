# FPL Live Rank

Live Fantasy Premier League scoring, mini-league rank movement, and effective ownership using public FPL endpoints and local calculations.

## Status

Phase 7 is in progress. The app now has manager live scoring, captaincy projection, formation-aware auto-subs, exact mini-league live rank, SignalR updates, Hangfire refresh jobs, Redis snapshot caching, effective ownership, rank-change explanations, Docker support, Swagger UI, and EF Core migrations.

## Tech Stack

- .NET 10 Web API with Clean Architecture: `Api`, `Application`, `Domain`, `Infrastructure`.
- EF Core 10 + Npgsql for relational persistence and migrations.
- PostgreSQL for app data and Hangfire job storage.
- Redis via StackExchange.Redis for upstream FPL response caching and short-lived computed snapshots.
- Hangfire recurring jobs for live event refreshes.
- SignalR for live manager, league, and event update streams.
- Polly retry policy for transient FPL failures, including `Retry-After` handling for 429/503.
- Angular 19, standalone components, signals, TypeScript, Tailwind CSS.
- xUnit, FluentAssertions, Karma/Jasmine, and coverlet.

## Environment Variables

The API reads normal ASP.NET Core configuration keys, so each setting can be supplied through `appsettings*.json`, environment variables, user secrets, or Docker Compose.


| Key                           | Default                                                                       | Notes                                                                                           |
| ----------------------------- | ----------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------- |
| `ASPNETCORE_ENVIRONMENT`      | `Production`                                                                  | Set to `Development` to expose `/swagger` and `/openapi/v1.json`.                               |
| `ConnectionStrings__Postgres` | `Host=localhost;Port=5432;Database=fpllive;Username=fpllive;Password=fpllive` | Used by EF Core and Hangfire PostgreSQL storage.                                                |
| `Redis__ConnectionString`     | `localhost:6379`                                                              | Redis endpoint. Compose uses `redis:6379`.                                                      |
| `Redis__KeyPrefix`            | `fpl:`                                                                        | Prefix for all app cache keys.                                                                  |
| `Redis__Enabled`              | `true`                                                                        | Set `false` to use `NullCacheService`; useful for local debugging but much noisier against FPL. |
| `FplApi__BaseUrl`             | `https://fantasy.premierleague.com/api/`                                      | Public FPL API base URL.                                                                        |
| `FplApi__UserAgent`           | `FplLiveRank/1.0`                                                             | Sent on FPL requests.                                                                           |
| `FplApi__TimeoutSeconds`      | `15`                                                                          | HTTP timeout per request.                                                                       |
| `FplApi__RetryCount`          | `3`                                                                           | Polly retry attempts for 5xx, timeout, and 429 responses.                                       |
| `Cors__AllowedOrigins__0`     | `http://localhost:4200`                                                       | Add `http://localhost:8081` for the containerized web app.                                      |


## Run With Docker

```bash
docker compose up --build
```

Services:

- Web: `http://localhost:8081`
- API: `http://localhost:8080`
- Swagger UI: `http://localhost:8080/swagger`
- OpenAPI JSON: `http://localhost:8080/openapi/v1.json`
- Health: `http://localhost:8080/health`
- PostgreSQL: `localhost:5432`
- Redis: `localhost:6379`

The API container uses PostgreSQL for Hangfire storage. Hangfire creates its own tables automatically. The app EF tables are defined by the `InitialCreate` migration and can be applied from a local SDK shell with the command in the next section.

## Run Locally

Prerequisites:

- .NET 10 SDK
- Node.js 22+
- Docker, or local PostgreSQL 16+/17+ and Redis 7+

Start infrastructure:

```bash
docker compose up postgres redis
```

Restore and run the API:

```bash
dotnet restore FplLiveRank.slnx
dotnet run --project src/FplLiveRank.Api
```

Apply EF Core migrations:

```bash
dotnet tool install --global dotnet-ef
dotnet ef database update \
  --project src/FplLiveRank.Infrastructure \
  --startup-project src/FplLiveRank.Api \
  --context AppDbContext
```

Run the Angular app:

```bash
cd client
npm install
npm start
```

`ng serve` reads `client/proxy.conf.json` and proxies `/api` to `http://localhost:8080`. The production Docker web image serves the Angular dist on nginx and reverse-proxies `/api` plus `/hubs` to the API container.

## API Endpoints


| Method  | Path                                                                 | Description                                                                                           |
| ------- | -------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------- |
| `GET`   | `/health`                                                            | Liveness probe.                                                                                       |
| `GET`   | `/openapi/v1.json`                                                   | OpenAPI document in Development.                                                                      |
| `GET`   | `/swagger`                                                           | Swagger UI in Development.                                                                            |
| `GET`   | `/api/fpl/events/current`                                            | Current or upcoming gameweek from `bootstrap-static`.                                                 |
| `POST`  | `/api/fpl/bootstrap/sync`                                            | Forces bootstrap cache refresh.                                                                       |
| `GET`   | `/api/fpl/manager/{managerId}/leagues`                               | Manager profile and public classic leagues.                                                           |
| `GET`   | `/api/fpl/manager/{managerId}/live?eventId=`                         | Live gameweek score, season total, captaincy, auto-subs, and pick breakdown.                          |
| `GET`   | `/api/fpl/league/{leagueId}/live?eventId=`                           | Exact mini-league live rank table.                                                                    |
| `POST`  | `/api/fpl/league/{leagueId}/refresh?eventId=`                        | Forces a fresh league recompute under a Redis lock and broadcasts SignalR updates.                    |
| `GET`   | `/api/fpl/league/{leagueId}/effective-ownership?eventId=&managerId=` | Mini-league player ownership, captaincy, effective ownership, and optional user-specific rank impact. |
| SignalR | `/hubs/fpl-live`                                                     | Manager, league, and event update stream.                                                             |


## Calculation Deep Dive

`ManagerLiveScoreService.GetAsync(managerId, eventId?)`:

1. Resolve the gameweek from `IFplBootstrapService.GetCurrentEventAsync` when `eventId` is not supplied.
2. Read cached inputs in parallel: picks, event live data, fixtures, manager history, bootstrap player metadata, and manager entry profile.
3. Build live stat rows from `event/{id}/live`.
4. Run calculators in this order: `CaptaincyProjector`, then `AutoSubProjector`, then `LivePointsCalculator`.
5. Captaincy projection promotes the vice-captain only when the captain blanked and the captain's team has finished or is `finished_provisional`.
6. Auto-sub projection is formation-aware, never reuses a bench player twice, and is disabled for Bench Boost.
7. Live points are `sum(live_total_points * multiplier) - transferCost`.
8. Season total is the previous completed gameweek total plus live points after hits.

`LeagueLiveRankService.GetAsync(leagueId, eventId?)`:

1. Fetch all public classic-league standings pages.
2. Calculate each manager through the same manager-live pipeline with bounded concurrency.
3. Sort by live season total and expose official rank, live rank, movement, tie flags, and previous-snapshot explanations.
4. `RefreshAsync` stores the previous live snapshot in Redis before writing the fresh snapshot, enabling "why my rank changed" text on later reads.

`EffectiveOwnershipCalculator`:

1. Reads each manager's adjusted live pick multipliers after captaincy and auto-sub projection.
2. Ownership percent = managers owning the player / league manager count.
3. Captaincy percent = managers captaining the player / league manager count.
4. Effective ownership percent = total live multipliers for that player / league manager count.
5. If `managerId` is supplied, `RankImpactPerPoint` compares the user's multiplier to the league effective ownership.

## Caching and Background Work


| Key                               | TTL    |
| --------------------------------- | ------ |
| `bootstrap`                       | 30 min |
| `event:status`                    | 45 s   |
| `event:{id}:live`                 | 45 s   |
| `event:{id}:fixtures`             | 45 s   |
| `manager:{id}:entry`              | 10 min |
| `manager:{id}:event:{e}:picks`    | 2 min  |
| `manager:{id}:history`            | 10 min |
| `league:{id}:standings:page:{n}`  | 2 min  |
| `manager:{id}:event:{e}:live`     | 30 s   |
| `league:{id}:event:{e}:live`      | 30 s   |
| `league:{id}:event:{e}:live:prev` | 6 h    |
| `league:{id}:event:{e}:eo`        | 30 s   |


Redis keys are prefixed by `Redis:KeyPrefix`. Snapshot recomputation uses Redis `SET NX PX` locks to avoid stampedes. Hangfire runs `EventLiveRefreshJob` every minute and stores job state in PostgreSQL via `Hangfire.PostgreSql`.

## Tests and Reporting

Backend:

```bash
dotnet test FplLiveRank.slnx
```

TRX + coverage output:

```bash
dotnet test FplLiveRank.slnx \
  --logger "trx;LogFileName=fpl-live-rank.trx" \
  --collect:"XPlat Code Coverage" \
  --results-directory TestResults
```

Frontend:

```bash
cd client
npm run build
npm run test:ci
```

`npm run test:ci` uses `ChromeHeadless`; install Chrome/Chromium or set `CHROME_BIN` in minimal Linux environments.

Coverage includes calculator unit tests, service tests, Redis-shaped cache behavior, API smoke tests, and Karma smoke tests for the league live and effective ownership pages.

## Screenshots

Add screenshots here when the UI is stable:

- Manager live score page: `client/src/app/pages/manager-live`.
- Live mini-league rank page: `client/src/app/pages/league-live`.
- Effective ownership page: `client/src/app/pages/league-effective-ownership`.

## Project Layout

```text
src/
  FplLiveRank.Api/              # controllers, middleware, SignalR hub, host wiring
  FplLiveRank.Application/      # services, calculators, DTOs, FPL response models, interfaces
  FplLiveRank.Domain/           # entities, enums, value records
  FplLiveRank.Infrastructure/   # EF DbContext/migrations, FPL HttpClient, Redis cache, Polly policies
tests/
  FplLiveRank.UnitTests/
  FplLiveRank.IntegrationTests/
client/
  src/app/api/                  # Angular API services and DTO mirror types
  src/app/pages/manager-live/
  src/app/pages/league-live/
  src/app/pages/league-effective-ownership/
```

## Known Limitations

- The app still primarily computes and caches live views on demand. The EF schema exists, but snapshot write-behind into `LiveManagerScore` / `LivePlayerPoint` is not yet wired.
- First cold request for a large private league can fan out to many public FPL calls. Redis snapshots and locks reduce repeated load after the first request.
- No authentication is required because the app only uses public FPL endpoints and public manager/league IDs.

## Legal and Ethics

Uses public FPL endpoints only. Caches aggressively, honors upstream retry hints, and avoids proprietary paid-product branding, endpoints, or algorithms.