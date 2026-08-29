using DeepLearning.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeepLearning.Infrastructure.Persistence.Configurations
{
    public class FollowUpQuestionConfiguration : IEntityTypeConfiguration<FollowUpQuestion>
    {
        public void Configure(EntityTypeBuilder<FollowUpQuestion> builder)
        {
            builder.ToTable("follow_up_questions");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            builder.Property(x => x.ContextRef).HasMaxLength(100);
            builder.Property(x => x.QuestionText).IsRequired();
            builder.Property(x => x.Verdict).HasDefaultValue(Domain.Enums.FollowUpVerdict.pending);
            builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");

            builder.HasIndex(x => x.SubmissionId).HasDatabaseName("idx_follow_up_questions_sub");

            builder.HasOne(x => x.Submission)
                .WithMany()
                .HasForeignKey(x => x.SubmissionId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
