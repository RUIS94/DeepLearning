using DeepLearning.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeepLearning.Infrastructure.Persistence.Configurations
{
    public class AssessmentDimensionConfiguration : IEntityTypeConfiguration<AssessmentDimension>
    {
        public void Configure(EntityTypeBuilder<AssessmentDimension> builder)
        {
            builder.ToTable("assessment_dimensions");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            builder.Property(x => x.DimensionKey).HasMaxLength(50).IsRequired();
            builder.Property(x => x.DimensionName).HasMaxLength(100).IsRequired();
            builder.Property(x => x.PassThreshold).HasMaxLength(20);
            builder.Property(x => x.LevelDescriptions).HasColumnType("jsonb").IsRequired();
            builder.Property(x => x.RubricVersion).HasMaxLength(20).IsRequired();
            builder.Property(x => x.EffectiveFrom).HasDefaultValueSql("now()");
            builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");

            builder.HasIndex(x => new { x.ExamTypeId, x.DimensionKey, x.RubricVersion }).IsUnique();

            builder.HasOne(x => x.ExamType)
                .WithMany()
                .HasForeignKey(x => x.ExamTypeId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
