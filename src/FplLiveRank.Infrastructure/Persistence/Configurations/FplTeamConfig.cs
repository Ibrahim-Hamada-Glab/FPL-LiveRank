using FplLiveRank.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FplLiveRank.Infrastructure.Persistence.Configurations;

public sealed class FplTeamConfig : IEntityTypeConfiguration<FplTeam>
{
    public void Configure(EntityTypeBuilder<FplTeam> b)
    {
        b.ToTable("fpl_teams");
        b.HasKey(t => t.Id);
        b.Property(t => t.Id).ValueGeneratedNever();
        b.Property(t => t.Name).HasMaxLength(64).IsRequired();
        b.Property(t => t.ShortName).HasMaxLength(8).IsRequired();
    }
}
