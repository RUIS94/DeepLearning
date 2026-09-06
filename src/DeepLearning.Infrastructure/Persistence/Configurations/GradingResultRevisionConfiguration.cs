using DeepLearning.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeepLearning.Infrastructure.Persistence.Configurations
{
    public class GradingResultRevisionConfiguration : IEntityTypeConfiguration<GradingResultRevision>
    {
        public void Configure(EntityTypeBuilder<GradingResultRevision> builder)
        {
            builder.ToTable("grading_result_revisions", t =>
            {
                t.HasCheckConstraint("ck_grading_result_revisions_from_band_range", "from_band BETWEEN 1 AND 5");
                t.HasCheckConstraint("ck_grading_result_revisions_to_band_range", "to_band BETWEEN 1 AND 5");
            });
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            builder.Property(x => x.Reason).IsRequired();
            builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");

            builder.HasIndex(x => x.SubmissionId).HasDatabaseName("idx_grading_result_revisions_submission");

            builder.HasOne(x => x.Submission)
                .WithMany()
                .HasForeignKey(x => x.SubmissionId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(x => x.Dimension)
                .WithMany()
                .HasForeignKey(x => x.DimensionId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.TriggeredByFollowUpThread)
                .WithMany()
                .HasForeignKey(x => x.TriggeredByFollowUpThreadId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
