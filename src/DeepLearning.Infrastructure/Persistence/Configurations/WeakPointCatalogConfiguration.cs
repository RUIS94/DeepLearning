using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeepLearning.Infrastructure.Persistence.Configurations
{
    public class WeakPointCatalogConfiguration : IEntityTypeConfiguration<WeakPointCatalog>
    {
        public void Configure(EntityTypeBuilder<WeakPointCatalog> builder)
        {
            builder.ToTable("weak_point_catalog");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            builder.Property(x => x.Code).HasMaxLength(60).IsRequired();
            builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
            builder.Property(x => x.Description).IsRequired();
            builder.Property(x => x.DefaultDimensionKey).HasMaxLength(50);
            builder.Property(x => x.DefaultErrorCategory).HasMaxLength(50);
            // Not the enum's ordinal-0 member (that's 'proposed'), so ValueGeneratedNever keeps
            // EF from discarding an explicit non-default on insert — same landmine as WeakPoint.Priority.
            builder.Property(x => x.Status).HasDefaultValue(WeakPointCatalogStatus.active).ValueGeneratedNever();
            builder.Property(x => x.Origin).HasMaxLength(20).HasDefaultValue("seed").ValueGeneratedNever();
            builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");

            builder.HasIndex(x => x.Code)
                .IsUnique()
                .HasDatabaseName("ux_weak_point_catalog_code");

            builder.HasOne(x => x.Category)
                .WithMany()
                .HasForeignKey(x => x.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
