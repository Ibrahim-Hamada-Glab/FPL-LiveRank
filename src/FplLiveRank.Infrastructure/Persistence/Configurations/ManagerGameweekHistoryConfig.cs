using FplLiveRank.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FplLiveRank.Infrastructure.Persistence.Configurations;

public sealed class ManagerGameweekHistoryConfig : IEntityTypeConfiguration<ManagerGameweekHistory>
{
    public void Configure(EntityTypeBuilder<ManagerGameweekHistory> b)
    {
        b.ToTable("manager_gameweek_history");
        b.HasKey(h => h.Id);
        b.HasIndex(h => new { h.ManagerId, h.EventId }).IsUnique();
    }
}
