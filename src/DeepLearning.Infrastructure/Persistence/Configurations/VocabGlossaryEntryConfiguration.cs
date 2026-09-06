using DeepLearning.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeepLearning.Infrastructure.Persistence.Configurations
{
    public class VocabGlossaryEntryConfiguration : IEntityTypeConfiguration<VocabGlossaryEntry>
    {
        public void Configure(EntityTypeBuilder<VocabGlossaryEntry> builder)
        {
            builder.ToTable("vocab_glossary");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            builder.Property(x => x.CanonicalKey).HasMaxLength(255).IsRequired();
            builder.Property(x => x.EnglishExpr).HasMaxLength(255).IsRequired();
            builder.Property(x => x.ChineseEquiv).HasMaxLength(255);
            builder.Property(x => x.Category).HasMaxLength(50);
            builder.Property(x => x.Domain).HasMaxLength(50);
            builder.Property(x => x.Scenario).HasMaxLength(100);
            builder.Property(x => x.FrequencyTag).HasMaxLength(20);
            builder.Property(x => x.SenseCount).HasDefaultValue(1);
            builder.Property(x => x.OccurrenceCount).HasDefaultValue(1);
            builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
            builder.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");

            builder.HasIndex(x => x.CanonicalKey).IsUnique().HasDatabaseName("idx_vocab_glossary_canonical");
        }
    }
}
