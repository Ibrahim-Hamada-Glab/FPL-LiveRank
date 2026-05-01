using FluentAssertions;
using FplLiveRank.Application.Interfaces;
using FplLiveRank.Infrastructure;
using FplLiveRank.Infrastructure.Cache;
using FplLiveRank.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json.Nodes;

namespace FplLiveRank.UnitTests.Infrastructure;

public sealed class InfrastructureConfigurationTests
{
    [Fact]
    public void AddInfrastructure_uses_configured_postgres_connection_string()
    {
        const string connectionString = "Host=db.example;Port=5432;Database=fpllive;Username=fpllive;Password=fpllive";
        using var provider = BuildProvider(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Postgres"] = connectionString,
            ["Redis:Enabled"] = "false"
        });

        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        db.Database.GetConnectionString().Should().Be(connectionString);
        provider.GetRequiredService<ICacheService>().Should().BeOfType<NullCacheService>();
    }

    [Fact]
    public void Appsettings_keep_local_postgres_and_redis_enabled()
    {
        var root = FindRepoRoot();
        var appsettings = JsonNode.Parse(File.ReadAllText(Path.Combine(root, "src/FplLiveRank.Api/appsettings.json")))!;
        var development = JsonNode.Parse(File.ReadAllText(Path.Combine(root, "src/FplLiveRank.Api/appsettings.Development.json")))!;

        appsettings["ConnectionStrings"]!["Postgres"]!.GetValue<string>()
            .Should()
            .Be("Host=localhost;Port=5432;Database=fpllive;Username=fpllive;Password=fpllive");
        appsettings["Redis"]!["ConnectionString"]!.GetValue<string>().Should().Be("localhost:6379");
        appsettings["Redis"]!["Enabled"]!.GetValue<bool>().Should().BeTrue();
        development["Redis"]!["Enabled"]!.GetValue<bool>().Should().BeTrue();
    }

    private static ServiceProvider BuildProvider(IReadOnlyDictionary<string, string?> values)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        return new ServiceCollection()
            .AddInfrastructure(config)
            .BuildServiceProvider(validateScopes: true);
    }

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "src/FplLiveRank.Api/appsettings.json")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }
}
