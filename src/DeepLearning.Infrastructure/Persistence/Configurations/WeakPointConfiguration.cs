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
            // Nullable now: set only for legacy (catalog-less) buckets. A catalog-mapped weak
            // point carries identity in CatalogId and leaves this null.
            builder.Property(x => x.Category).HasMaxLength(100);
            builder.Property(x => x.FirstDetectedAt).HasDefaultValueSql("now()");
            builder.Property(x => x.LastSeenAt).HasDefaultValueSql("now()");
            builder.Property(x => x.RecurrenceCount).HasDefaultValue(0);
            builder.Property(x => x.Status).HasDefaultValue(Domain.Enums.WeakPointStatus.active).ValueGeneratedNever();
            // ValueGeneratedNever() matters here specifically: Priority.medium (the DB default) is
            // NOT the enum's ordinal-0 member (Priority.high is) — without this, EF's
            // "HasDefaultValue implies ValueGeneratedOnAdd" convention would silently discard an
            // explicit Priority.high on a brand-new row and substitute medium instead, the same
            // bug FollowUpQuestionConfiguration.Verdict hit for real (see its comment).
            builder.Property(x => x.Priority).HasDefaultValue(Domain.Enums.Priority.medium).ValueGeneratedNever();
            builder.Property(x => x.DetectionSource).HasMaxLength(20).HasDefaultValue("rule").ValueGeneratedNever();

            builder.HasIndex(x => new { x.UserId, x.Status }).HasDatabaseName("idx_weak_points_user_status");

            // Legacy (catalog-less) bucket dedup: one row per (user, category) among rows where
            // catalog_id IS NULL. Partial so it mirrors ux_weak_points_user_catalog and a
            // catalog-mapped row (category null) is governed ONLY by that other index — the two
            // never both apply to the same row, so promotion/merge writes have one collision
            // surface, not two.
            builder.HasIndex(x => new { x.UserId, x.Category })
                .IsUnique()
                .HasDatabaseName("ux_weak_points_user_category")
                .HasFilter("catalog_id IS NULL");

            // Catalog-based path — a catalog-matched weak point is looked up by (user, catalog_id)
            // before insert/update. Partial: legacy rows have catalog_id NULL.
            builder.HasIndex(x => new { x.UserId, x.CatalogId })
                .IsUnique()
                .HasDatabaseName("ux_weak_points_user_catalog")
                .HasFilter("catalog_id IS NOT NULL");

            builder.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.Catalog)
                .WithMany()
                .HasForeignKey(x => x.CatalogId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.HasOne<Domain.Entities.ExamType>()
                .WithMany()
                .HasForeignKey(x => x.ExamTypeId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
