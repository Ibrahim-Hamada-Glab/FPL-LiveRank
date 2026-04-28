using FplLiveRank.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FplLiveRank.Infrastructure.Persistence;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<FplEvent> Events => Set<FplEvent>();
    public DbSet<FplTeam> Teams => Set<FplTeam>();
    public DbSet<FplPlayer> Players => Set<FplPlayer>();
    public DbSet<FplManager> Managers => Set<FplManager>();
    public DbSet<ManagerGameweekPick> ManagerPicks => Set<ManagerGameweekPick>();
    public DbSet<ManagerGameweekHistory> ManagerHistory => Set<ManagerGameweekHistory>();
    public DbSet<LivePlayerPoint> LivePlayerPoints => Set<LivePlayerPoint>();
    public DbSet<LiveManagerScore> LiveManagerScores => Set<LiveManagerScore>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
