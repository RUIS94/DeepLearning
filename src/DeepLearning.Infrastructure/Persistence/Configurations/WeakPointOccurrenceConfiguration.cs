using DeepLearning.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeepLearning.Infrastructure.Persistence.Configurations
{
    public class WeakPointOccurrenceConfiguration : IEntityTypeConfiguration<WeakPointOccurrence>
    {
        public void Configure(EntityTypeBuilder<WeakPointOccurrence> builder)
        {
            builder.ToTable("weak_point_occurrences");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            builder.Property(x => x.IsRecurrence).HasDefaultValue(false);
            builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");

            builder.HasIndex(x => x.WeakPointId).HasDatabaseName("idx_weak_point_occurrences_wp");

            // A weak point occurs at most once per submission — UpdateWeakPointsOnGraded already
            // dedups buckets in memory per grading; this is the DB backstop against a re-grade
            // (a second SubmissionGradedEvent) or a concurrent event double-inserting and
            // inflating occurrence / recurrence counts. Writers use ON CONFLICT DO NOTHING.
            builder.HasIndex(x => new { x.WeakPointId, x.SubmissionId })
                .IsUnique()
                .HasDatabaseName("ux_weak_point_occurrences_wp_submission");

            builder.HasOne(x => x.WeakPoint)
                .WithMany()
                .HasForeignKey(x => x.WeakPointId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(x => x.Submission)
                .WithMany()
                .HasForeignKey(x => x.SubmissionId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(x => x.ErrorList)
                .WithMany()
                .HasForeignKey(x => x.ErrorListId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}
