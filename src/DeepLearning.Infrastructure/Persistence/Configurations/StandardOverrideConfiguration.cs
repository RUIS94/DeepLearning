using DeepLearning.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeepLearning.Infrastructure.Persistence.Configurations
{
    public class StandardOverrideConfiguration : IEntityTypeConfiguration<StandardOverride>
    {
        public void Configure(EntityTypeBuilder<StandardOverride> builder)
        {
            builder.ToTable("standard_overrides");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            builder.Property(x => x.DimensionOrRule).HasMaxLength(100).IsRequired();
            builder.Property(x => x.RevisedRuleText).IsRequired();
            builder.Property(x => x.Status).HasDefaultValue(Domain.Enums.OverrideStatus.observing).ValueGeneratedNever();
            builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");

            builder.HasIndex(x => x.Status).HasDatabaseName("idx_standard_overrides_status");

            builder.HasOne(x => x.TriggeredByFollowup)
                .WithMany()
                .HasForeignKey(x => x.TriggeredByFollowupId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.TriggeredByFollowUpThread)
                .WithMany()
                .HasForeignKey(x => x.TriggeredByFollowUpThreadId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.PreviousOverride)
                .WithMany()
                .HasForeignKey(x => x.PreviousOverrideId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
