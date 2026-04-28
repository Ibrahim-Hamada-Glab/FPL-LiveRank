using FplLiveRank.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FplLiveRank.Infrastructure.Persistence.Configurations;

public sealed class LivePlayerPointConfig : IEntityTypeConfiguration<LivePlayerPoint>
{
    public void Configure(EntityTypeBuilder<LivePlayerPoint> b)
    {
        b.ToTable("live_player_points");
        b.HasKey(p => p.Id);
        b.HasIndex(p => new { p.EventId, p.ElementId }).IsUnique();
        b.Property(p => p.Influence).HasPrecision(8, 2);
        b.Property(p => p.Creativity).HasPrecision(8, 2);
        b.Property(p => p.Threat).HasPrecision(8, 2);
        b.Property(p => p.IctIndex).HasPrecision(8, 2);
    }
}
