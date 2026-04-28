using FplLiveRank.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FplLiveRank.Infrastructure.Persistence.Configurations;

public sealed class FplPlayerConfig : IEntityTypeConfiguration<FplPlayer>
{
    public void Configure(EntityTypeBuilder<FplPlayer> b)
    {
        b.ToTable("fpl_players");
        b.HasKey(p => p.Id);
        b.Property(p => p.Id).ValueGeneratedNever();
        b.Property(p => p.WebName).HasMaxLength(64).IsRequired();
        b.Property(p => p.FirstName).HasMaxLength(64);
        b.Property(p => p.SecondName).HasMaxLength(64);
        b.Property(p => p.Status).HasMaxLength(8);
        b.Property(p => p.SelectedByPercent).HasPrecision(6, 2);
        b.HasIndex(p => p.TeamId);
    }
}
