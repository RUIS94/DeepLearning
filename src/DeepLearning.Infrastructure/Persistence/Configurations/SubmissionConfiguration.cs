using DeepLearning.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeepLearning.Infrastructure.Persistence.Configurations
{
    public class SubmissionConfiguration : IEntityTypeConfiguration<Submission>
    {
        public void Configure(EntityTypeBuilder<Submission> builder)
        {
            builder.ToTable("submissions");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            builder.Property(x => x.Content).HasColumnType("jsonb").IsRequired();
            builder.Property(x => x.Status).HasDefaultValue(Domain.Enums.SubmissionStatus.draft);
            builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
            builder.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");

            builder.HasIndex(x => x.UserId).HasDatabaseName("idx_submissions_user");
            builder.HasIndex(x => x.Status).HasDatabaseName("idx_submissions_status");

            builder.HasOne(x => x.Question)
                .WithMany()
                .HasForeignKey(x => x.QuestionId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
