using DeepLearning.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeepLearning.Infrastructure.Persistence.Configurations
{
    public class QuestionBankCategoryConfiguration : IEntityTypeConfiguration<QuestionBankCategory>
    {
        public void Configure(EntityTypeBuilder<QuestionBankCategory> builder)
        {
            builder.ToTable("question_bank_categories");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
            builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");

            builder.HasOne(x => x.Parent)
                .WithMany()
                .HasForeignKey(x => x.ParentId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
