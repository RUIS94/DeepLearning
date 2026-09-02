using DeepLearning.Domain.Entities;
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
            builder.Property(x => x.IsActive).HasDefaultValue(true).ValueGeneratedNever();
            builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");

            builder.HasIndex(x => new { x.ExamTypeId, x.Code })
                .IsUnique()
                .HasDatabaseName("ux_weak_point_catalog_exam_code");

            builder.HasOne(x => x.ExamType)
                .WithMany()
                .HasForeignKey(x => x.ExamTypeId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
