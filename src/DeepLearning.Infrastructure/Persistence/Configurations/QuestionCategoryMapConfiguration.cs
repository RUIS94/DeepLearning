using DeepLearning.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeepLearning.Infrastructure.Persistence.Configurations
{
    public class QuestionCategoryMapConfiguration : IEntityTypeConfiguration<QuestionCategoryMap>
    {
        public void Configure(EntityTypeBuilder<QuestionCategoryMap> builder)
        {
            builder.ToTable("question_category_map");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");

            builder.HasIndex(x => new { x.QuestionId, x.CategoryId }).IsUnique();

            builder.HasOne(x => x.Question)
                .WithMany()
                .HasForeignKey(x => x.QuestionId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(x => x.Category)
                .WithMany()
                .HasForeignKey(x => x.CategoryId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
