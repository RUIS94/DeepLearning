using DeepLearning.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeepLearning.Infrastructure.Persistence.Configurations
{
    public class ErrorTaxonomyConfiguration : IEntityTypeConfiguration<ErrorTaxonomy>
    {
        public void Configure(EntityTypeBuilder<ErrorTaxonomy> builder)
        {
            builder.ToTable("error_taxonomies");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            builder.Property(x => x.CategoryKey).HasMaxLength(50).IsRequired();
            builder.Property(x => x.CategoryName).HasMaxLength(100).IsRequired();
            builder.Property(x => x.ExampleCases).HasColumnType("jsonb");
            builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");

            builder.HasIndex(x => new { x.ExamTypeId, x.CategoryKey }).IsUnique();

            builder.HasOne(x => x.ExamType)
                .WithMany()
                .HasForeignKey(x => x.ExamTypeId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
