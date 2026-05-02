using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FplLiveRank.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "fpl_events",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DeadlineTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsCurrent = table.Column<bool>(type: "boolean", nullable: false),
                    IsNext = table.Column<bool>(type: "boolean", nullable: false),
                    IsFinished = table.Column<bool>(type: "boolean", nullable: false),
                    IsDataChecked = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fpl_events", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "fpl_managers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    PlayerName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    TeamName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    LastSyncedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fpl_managers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "fpl_players",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    WebName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    FirstName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SecondName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    TeamId = table.Column<int>(type: "integer", nullable: false),
                    ElementType = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    NowCost = table.Column<int>(type: "integer", nullable: false),
                    SelectedByPercent = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fpl_players", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "fpl_teams",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ShortName = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fpl_teams", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "live_manager_scores",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ManagerId = table.Column<int>(type: "integer", nullable: false),
                    EventId = table.Column<int>(type: "integer", nullable: false),
                    RawLivePoints = table.Column<int>(type: "integer", nullable: false),
                    TransferCost = table.Column<int>(type: "integer", nullable: false),
                    LivePointsAfterHits = table.Column<int>(type: "integer", nullable: false),
                    PreviousTotal = table.Column<int>(type: "integer", nullable: false),
                    LiveSeasonTotal = table.Column<int>(type: "integer", nullable: false),
                    ProjectedAutoSubsJson = table.Column<string>(type: "jsonb", nullable: true),
                    ActiveChip = table.Column<int>(type: "integer", nullable: false),
                    CalculatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_live_manager_scores", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "live_player_points",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EventId = table.Column<int>(type: "integer", nullable: false),
                    ElementId = table.Column<int>(type: "integer", nullable: false),
                    TotalPoints = table.Column<int>(type: "integer", nullable: false),
                    Minutes = table.Column<int>(type: "integer", nullable: false),
                    GoalsScored = table.Column<int>(type: "integer", nullable: false),
                    Assists = table.Column<int>(type: "integer", nullable: false),
                    CleanSheets = table.Column<int>(type: "integer", nullable: false),
                    GoalsConceded = table.Column<int>(type: "integer", nullable: false),
                    OwnGoals = table.Column<int>(type: "integer", nullable: false),
                    PenaltiesSaved = table.Column<int>(type: "integer", nullable: false),
                    PenaltiesMissed = table.Column<int>(type: "integer", nullable: false),
                    YellowCards = table.Column<int>(type: "integer", nullable: false),
                    RedCards = table.Column<int>(type: "integer", nullable: false),
                    Saves = table.Column<int>(type: "integer", nullable: false),
                    Bonus = table.Column<int>(type: "integer", nullable: false),
                    Bps = table.Column<int>(type: "integer", nullable: false),
                    Influence = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    Creativity = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    Threat = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    IctIndex = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_live_player_points", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "manager_gameweek_history",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ManagerId = table.Column<int>(type: "integer", nullable: false),
                    EventId = table.Column<int>(type: "integer", nullable: false),
                    Points = table.Column<int>(type: "integer", nullable: false),
                    TotalPoints = table.Column<int>(type: "integer", nullable: false),
                    Rank = table.Column<int>(type: "integer", nullable: false),
                    OverallRank = table.Column<int>(type: "integer", nullable: false),
                    Bank = table.Column<int>(type: "integer", nullable: false),
                    Value = table.Column<int>(type: "integer", nullable: false),
                    EventTransfers = table.Column<int>(type: "integer", nullable: false),
                    EventTransfersCost = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_manager_gameweek_history", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "manager_gameweek_picks",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ManagerId = table.Column<int>(type: "integer", nullable: false),
                    EventId = table.Column<int>(type: "integer", nullable: false),
                    ElementId = table.Column<int>(type: "integer", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    Multiplier = table.Column<int>(type: "integer", nullable: false),
                    IsCaptain = table.Column<bool>(type: "boolean", nullable: false),
                    IsViceCaptain = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_manager_gameweek_picks", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_fpl_events_IsCurrent",
                table: "fpl_events",
                column: "IsCurrent");

            migrationBuilder.CreateIndex(
                name: "IX_fpl_players_TeamId",
                table: "fpl_players",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_live_manager_scores_ManagerId_EventId",
                table: "live_manager_scores",
                columns: new[] { "ManagerId", "EventId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_live_player_points_EventId_ElementId",
                table: "live_player_points",
                columns: new[] { "EventId", "ElementId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_manager_gameweek_history_ManagerId_EventId",
                table: "manager_gameweek_history",
                columns: new[] { "ManagerId", "EventId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_manager_gameweek_picks_ManagerId_EventId",
                table: "manager_gameweek_picks",
                columns: new[] { "ManagerId", "EventId" });

            migrationBuilder.CreateIndex(
                name: "IX_manager_gameweek_picks_ManagerId_EventId_ElementId",
                table: "manager_gameweek_picks",
                columns: new[] { "ManagerId", "EventId", "ElementId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "fpl_events");

            migrationBuilder.DropTable(
                name: "fpl_managers");

            migrationBuilder.DropTable(
                name: "fpl_players");

            migrationBuilder.DropTable(
                name: "fpl_teams");

            migrationBuilder.DropTable(
                name: "live_manager_scores");

            migrationBuilder.DropTable(
                name: "live_player_points");

            migrationBuilder.DropTable(
                name: "manager_gameweek_history");

            migrationBuilder.DropTable(
                name: "manager_gameweek_picks");
        }
    }
}
