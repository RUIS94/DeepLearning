using DeepLearning.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeepLearning.Infrastructure.Persistence.Configurations
{
    public class WeakPointConfiguration : IEntityTypeConfiguration<WeakPoint>
    {
        public void Configure(EntityTypeBuilder<WeakPoint> builder)
        {
            builder.ToTable("weak_points");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            builder.Property(x => x.Category).HasMaxLength(100).IsRequired();
            builder.Property(x => x.FirstDetectedAt).HasDefaultValueSql("now()");
            builder.Property(x => x.LastSeenAt).HasDefaultValueSql("now()");
            builder.Property(x => x.RecurrenceCount).HasDefaultValue(0);
            builder.Property(x => x.Status).HasDefaultValue(Domain.Enums.WeakPointStatus.active);
            builder.Property(x => x.Priority).HasDefaultValue(Domain.Enums.Priority.medium);

            builder.HasIndex(x => new { x.UserId, x.Status }).HasDatabaseName("idx_weak_points_user_status");

            builder.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
