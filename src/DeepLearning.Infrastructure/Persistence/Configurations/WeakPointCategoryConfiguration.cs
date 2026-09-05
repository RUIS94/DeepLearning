using DeepLearning.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeepLearning.Infrastructure.Persistence.Configurations
{
    public class WeakPointCategoryConfiguration : IEntityTypeConfiguration<WeakPointCategory>
    {
        public void Configure(EntityTypeBuilder<WeakPointCategory> builder)
        {
            builder.ToTable("weak_point_categories");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            builder.Property(x => x.Code).HasMaxLength(60).IsRequired();
            builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
            builder.Property(x => x.DisplayOrder).HasDefaultValue(0);

            builder.HasIndex(x => x.Code).IsUnique().HasDatabaseName("ux_weak_point_categories_code");
        }
    }
}
