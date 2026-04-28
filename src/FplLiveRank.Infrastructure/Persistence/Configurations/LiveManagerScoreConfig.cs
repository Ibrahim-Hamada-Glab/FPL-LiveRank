using FplLiveRank.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FplLiveRank.Infrastructure.Persistence.Configurations;

public sealed class LiveManagerScoreConfig : IEntityTypeConfiguration<LiveManagerScore>
{
    public void Configure(EntityTypeBuilder<LiveManagerScore> b)
    {
        b.ToTable("live_manager_scores");
        b.HasKey(s => s.Id);
        b.HasIndex(s => new { s.ManagerId, s.EventId }).IsUnique();
        b.Property(s => s.ProjectedAutoSubsJson).HasColumnType("jsonb");
        b.Property(s => s.ActiveChip).HasConversion<int>();
    }
}
