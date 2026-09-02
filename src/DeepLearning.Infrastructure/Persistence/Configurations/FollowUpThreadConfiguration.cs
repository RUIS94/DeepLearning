using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeepLearning.Infrastructure.Persistence.Configurations
{
    public class FollowUpThreadConfiguration : IEntityTypeConfiguration<FollowUpThread>
    {
        public void Configure(EntityTypeBuilder<FollowUpThread> builder)
        {
            builder.ToTable("follow_up_threads");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            builder.Property(x => x.ContextRef).HasMaxLength(100);
            // Same EF "HasDefaultValue implies omit-if-CLR-default" footgun as
            // FollowUpQuestionConfiguration.Verdict — open is ordinal 0, so ValueGeneratedNever()
            // is required or a brand-new closed... no, a brand-new *open* thread would silently
            // rely on the DB default anyway; the real risk is any future non-zero-default status
            // getting dropped from the INSERT. Kept for consistency with that documented fix.
            builder.Property(x => x.Status).HasDefaultValue(FollowUpThreadStatus.open).ValueGeneratedNever();
            builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");

            // A submission can have many threads over time (design revision, 2026-09-02): once a
            // thread closes, the user may start another, unrelated one — but only one may be
            // *open* at a time (CreateFollowUpThreadCommandHandler enforces that in code, so the
            // submission's under_dispute state maps cleanly to "has exactly one open thread").
            builder.HasIndex(x => x.SubmissionId).HasDatabaseName("ix_follow_up_threads_submission");

            builder.HasOne(x => x.Submission)
                .WithMany()
                .HasForeignKey(x => x.SubmissionId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.ExamType)
                .WithMany()
                .HasForeignKey(x => x.ExamTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            // StandardOverrideId is a plain denormalized pointer, not a formal EF relationship —
            // the authoritative link is standard_overrides.triggered_by_follow_up_thread_id
            // (the other direction, mirroring TriggeredByFollowupId's existing precedent).
            // CloseFollowUpThreadCommandHandler sets both in the same transaction.
        }
    }
}
