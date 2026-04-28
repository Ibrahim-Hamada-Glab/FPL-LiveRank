using FplLiveRank.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FplLiveRank.Infrastructure.Persistence.Configurations;

public sealed class FplEventConfig : IEntityTypeConfiguration<FplEvent>
{
    public void Configure(EntityTypeBuilder<FplEvent> b)
    {
        b.ToTable("fpl_events");
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).ValueGeneratedNever();
        b.Property(e => e.Name).HasMaxLength(64).IsRequired();
        b.HasIndex(e => e.IsCurrent);
    }
}
