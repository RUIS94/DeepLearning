using DeepLearning.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeepLearning.Infrastructure.Persistence.Configurations
{
    public class QuestionConfiguration : IEntityTypeConfiguration<Question>
    {
        public void Configure(EntityTypeBuilder<Question> builder)
        {
            builder.ToTable("questions");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            builder.Property(x => x.Title).HasMaxLength(255).IsRequired();
            builder.Property(x => x.Brief).HasColumnType("jsonb");
            builder.Property(x => x.BriefDomain).HasMaxLength(100);
            builder.Property(x => x.BriefTextType).HasMaxLength(100);
            builder.Property(x => x.BriefPurpose);
            builder.Property(x => x.BriefAudience).HasMaxLength(200);
            builder.Property(x => x.SourceText).IsRequired();
            builder.Property(x => x.IsSeedReference).HasDefaultValue(false);
            builder.Property(x => x.InBank).HasDefaultValue(false);
            builder.Property(x => x.Visibility).HasDefaultValue(Domain.Enums.Visibility.Private).ValueGeneratedNever();
            builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
            builder.Property(x => x.IsActive).HasDefaultValue(true);

            builder.HasIndex(x => x.InBank).HasDatabaseName("idx_questions_in_bank").HasFilter("in_bank = true");
            builder.HasIndex(x => new { x.TaskType, x.Difficulty }).HasDatabaseName("idx_questions_task_difficulty");

            builder.HasOne(x => x.Creator)
                .WithMany()
                .HasForeignKey(x => x.CreatedBy)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
