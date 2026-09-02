using DeepLearning.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeepLearning.Infrastructure.Persistence.Configurations
{
    public class GradingSummaryConfiguration : IEntityTypeConfiguration<GradingSummary>
    {
        public void Configure(EntityTypeBuilder<GradingSummary> builder)
        {
            builder.ToTable("grading_summaries");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            builder.Property(x => x.OverallPassProbability).HasColumnType("numeric(5,4)");
            builder.Property(x => x.CumulativeDensityFlag).HasDefaultValue(false);
            builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");

            builder.HasIndex(x => x.SubmissionId)
                .IsUnique()
                .HasDatabaseName("ux_grading_summaries_submission");

            builder.HasOne(x => x.Submission)
                .WithMany()
                .HasForeignKey(x => x.SubmissionId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
