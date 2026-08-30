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
            builder.Property(x => x.Status).HasDefaultValue(Domain.Enums.SubmissionStatus.draft).ValueGeneratedNever();
            builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
            builder.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");

            // Postgres's built-in xmin system column as an optimistic concurrency token — no new
            // column, no migration needed. Closes a real race in GradeSubmissionCommandHandler:
            // Submission.TransitionTo(grading)'s in-memory state-machine check only guards
            // sequential calls (a second call arriving after the first's SaveChangesAsync already
            // committed sees Grading and 409s) — two calls that both read Submitted before either
            // commits would otherwise both pass the check and both persist. With xmin as a
            // concurrency token, EF adds "WHERE xmin = @original" to the UPDATE, so whichever
            // SaveChangesAsync loses the race throws DbUpdateConcurrencyException instead of
            // silently succeeding — GradeSubmissionCommandHandler translates that into a 409.
            builder.Property<uint>("xmin")
                .HasColumnName("xmin")
                .HasColumnType("xid")
                .ValueGeneratedOnAddOrUpdate()
                .IsRowVersion();

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
