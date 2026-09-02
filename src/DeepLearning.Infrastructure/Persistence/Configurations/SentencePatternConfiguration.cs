using DeepLearning.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeepLearning.Infrastructure.Persistence.Configurations
{
    public class SentencePatternConfiguration : IEntityTypeConfiguration<SentencePattern>
    {
        public void Configure(EntityTypeBuilder<SentencePattern> builder)
        {
            builder.ToTable("sentence_patterns");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            builder.Property(x => x.PatternName).HasMaxLength(100).IsRequired();
            builder.Property(x => x.BreakdownSteps).HasColumnType("jsonb");
            builder.Property(x => x.Domain).HasMaxLength(50);
            builder.Property(x => x.Scenario).HasMaxLength(100);
            builder.Property(x => x.FrequencyTag).HasMaxLength(20);
            builder.Property(x => x.CanonicalKey).HasMaxLength(255);
            builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");

            builder.HasIndex(x => x.CanonicalKey).HasDatabaseName("idx_pattern_canonical");

            builder.HasOne(x => x.Question)
                .WithMany()
                .HasForeignKey(x => x.QuestionId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}
