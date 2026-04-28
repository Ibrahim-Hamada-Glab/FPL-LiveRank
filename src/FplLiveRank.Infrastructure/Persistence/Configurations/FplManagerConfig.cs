using FplLiveRank.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FplLiveRank.Infrastructure.Persistence.Configurations;

public sealed class FplManagerConfig : IEntityTypeConfiguration<FplManager>
{
    public void Configure(EntityTypeBuilder<FplManager> b)
    {
        b.ToTable("fpl_managers");
        b.HasKey(m => m.Id);
        b.Property(m => m.Id).ValueGeneratedNever();
        b.Property(m => m.PlayerName).HasMaxLength(128);
        b.Property(m => m.TeamName).HasMaxLength(128);
    }
}
