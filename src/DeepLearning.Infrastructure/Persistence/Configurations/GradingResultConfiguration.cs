using DeepLearning.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeepLearning.Infrastructure.Persistence.Configurations
{
    public class GradingResultConfiguration : IEntityTypeConfiguration<GradingResult>
    {
        public void Configure(EntityTypeBuilder<GradingResult> builder)
        {
            builder.ToTable("grading_results", t =>
            {
                t.HasCheckConstraint("ck_grading_results_band_range", "band BETWEEN 1 AND 5");
                t.HasCheckConstraint(
                    "ck_grading_results_alternative_band_range",
                    "alternative_band IS NULL OR alternative_band BETWEEN 1 AND 5");
            });
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            builder.Property(x => x.RubricVersion).HasMaxLength(20).IsRequired();
            builder.Property(x => x.Rationale).IsRequired();
            builder.Property(x => x.CumulativeDensityFlag).HasDefaultValue(false);
            builder.Property(x => x.EstimatedPassProbability).HasPrecision(5, 2);
            builder.Property(x => x.Confidence).HasMaxLength(10);
            builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");

            builder.HasIndex(x => x.SubmissionId).HasDatabaseName("idx_grading_results_submission");

            builder.HasOne(x => x.Submission)
                .WithMany()
                .HasForeignKey(x => x.SubmissionId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(x => x.Dimension)
                .WithMany()
                .HasForeignKey(x => x.DimensionId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
