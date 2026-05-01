using FluentAssertions;
using FplLiveRank.Domain.Entities;
using FplLiveRank.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FplLiveRank.UnitTests.Infrastructure;

public sealed class AppDbContextModelTests
{
    [Fact]
    public void Model_maps_core_tables_and_indexes_for_postgres()
    {
        using var db = CreateContext();
        var model = db.Model;

        model.FindEntityType(typeof(FplEvent))!.GetTableName().Should().Be("fpl_events");
        model.FindEntityType(typeof(FplTeam))!.GetTableName().Should().Be("fpl_teams");
        model.FindEntityType(typeof(FplPlayer))!.GetTableName().Should().Be("fpl_players");
        model.FindEntityType(typeof(FplManager))!.GetTableName().Should().Be("fpl_managers");
        model.FindEntityType(typeof(ManagerGameweekPick))!.GetTableName().Should().Be("manager_gameweek_picks");
        model.FindEntityType(typeof(ManagerGameweekHistory))!.GetTableName().Should().Be("manager_gameweek_history");
        model.FindEntityType(typeof(LivePlayerPoint))!.GetTableName().Should().Be("live_player_points");
        model.FindEntityType(typeof(LiveManagerScore))!.GetTableName().Should().Be("live_manager_scores");

        model.FindEntityType(typeof(ManagerGameweekPick))!
            .GetIndexes()
            .Should()
            .Contain(i => i.IsUnique && i.Properties.Select(p => p.Name).SequenceEqual(new[]
            {
                nameof(ManagerGameweekPick.ManagerId),
                nameof(ManagerGameweekPick.EventId),
                nameof(ManagerGameweekPick.ElementId)
            }));

        model.FindEntityType(typeof(LiveManagerScore))!
            .GetIndexes()
            .Should()
            .Contain(i => i.IsUnique && i.Properties.Select(p => p.Name).SequenceEqual(new[]
            {
                nameof(LiveManagerScore.ManagerId),
                nameof(LiveManagerScore.EventId)
            }));
    }

    [Fact]
    public void Model_uses_postgres_specific_types_for_live_score_payloads()
    {
        using var db = CreateContext();

        var liveManagerScore = db.Model.FindEntityType(typeof(LiveManagerScore))!;
        liveManagerScore.FindProperty(nameof(LiveManagerScore.ProjectedAutoSubsJson))!
            .GetColumnType()
            .Should()
            .Be("jsonb");
        liveManagerScore.FindProperty(nameof(LiveManagerScore.ActiveChip))!
            .GetProviderClrType()
            .Should()
            .Be(typeof(int));

        var livePlayerPoint = db.Model.FindEntityType(typeof(LivePlayerPoint))!;
        livePlayerPoint.FindProperty(nameof(LivePlayerPoint.Influence))!.GetPrecision().Should().Be(8);
        livePlayerPoint.FindProperty(nameof(LivePlayerPoint.Influence))!.GetScale().Should().Be(2);
        livePlayerPoint.FindProperty(nameof(LivePlayerPoint.Creativity))!.GetPrecision().Should().Be(8);
        livePlayerPoint.FindProperty(nameof(LivePlayerPoint.Threat))!.GetPrecision().Should().Be(8);
        livePlayerPoint.FindProperty(nameof(LivePlayerPoint.IctIndex))!.GetPrecision().Should().Be(8);
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=fpllive;Username=fpllive;Password=fpllive")
            .Options;

        return new AppDbContext(options);
    }
}
