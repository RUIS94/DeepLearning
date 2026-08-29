using DeepLearning.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeepLearning.Infrastructure.Persistence.Configurations
{
    public class WeakPointOccurrenceConfiguration : IEntityTypeConfiguration<WeakPointOccurrence>
    {
        public void Configure(EntityTypeBuilder<WeakPointOccurrence> builder)
        {
            builder.ToTable("weak_point_occurrences");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            builder.Property(x => x.IsRecurrence).HasDefaultValue(false);
            builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");

            builder.HasIndex(x => x.WeakPointId).HasDatabaseName("idx_weak_point_occurrences_wp");

            builder.HasOne(x => x.WeakPoint)
                .WithMany()
                .HasForeignKey(x => x.WeakPointId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(x => x.Submission)
                .WithMany()
                .HasForeignKey(x => x.SubmissionId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(x => x.ErrorList)
                .WithMany()
                .HasForeignKey(x => x.ErrorListId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}
