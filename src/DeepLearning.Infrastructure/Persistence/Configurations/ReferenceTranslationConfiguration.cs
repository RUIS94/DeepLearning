using DeepLearning.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeepLearning.Infrastructure.Persistence.Configurations
{
    public class ReferenceTranslationConfiguration : IEntityTypeConfiguration<ReferenceTranslation>
    {
        public void Configure(EntityTypeBuilder<ReferenceTranslation> builder)
        {
            builder.ToTable("reference_translations");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            builder.Property(x => x.ReferenceText).IsRequired();
            builder.Property(x => x.ComparisonNotes).HasColumnType("jsonb");
            builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");

            builder.HasOne(x => x.Question)
                .WithMany()
                .HasForeignKey(x => x.QuestionId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
