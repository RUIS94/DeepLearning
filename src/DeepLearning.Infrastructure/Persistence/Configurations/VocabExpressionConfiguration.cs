using DeepLearning.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeepLearning.Infrastructure.Persistence.Configurations
{
    public class VocabExpressionConfiguration : IEntityTypeConfiguration<VocabExpression>
    {
        public void Configure(EntityTypeBuilder<VocabExpression> builder)
        {
            builder.ToTable("vocab_expressions");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            builder.Property(x => x.EnglishExpr).HasMaxLength(255).IsRequired();
            builder.Property(x => x.ChineseEquiv).HasMaxLength(255);
            builder.Property(x => x.Category).HasMaxLength(50);
            builder.Property(x => x.Domain).HasMaxLength(50);
            builder.Property(x => x.Scenario).HasMaxLength(100);
            builder.Property(x => x.FrequencyTag).HasMaxLength(20);
            builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");

            builder.HasOne(x => x.Question)
                .WithMany()
                .HasForeignKey(x => x.QuestionId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}
