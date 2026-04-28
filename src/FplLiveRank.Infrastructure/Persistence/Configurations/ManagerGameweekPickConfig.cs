using FplLiveRank.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FplLiveRank.Infrastructure.Persistence.Configurations;

public sealed class ManagerGameweekPickConfig : IEntityTypeConfiguration<ManagerGameweekPick>
{
    public void Configure(EntityTypeBuilder<ManagerGameweekPick> b)
    {
        b.ToTable("manager_gameweek_picks");
        b.HasKey(p => p.Id);
        b.HasIndex(p => new { p.ManagerId, p.EventId });
        b.HasIndex(p => new { p.ManagerId, p.EventId, p.ElementId }).IsUnique();
    }
}
