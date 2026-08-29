using DeepLearning.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeepLearning.Infrastructure.Persistence.Configurations
{
    public class FeatureFlagConfiguration : IEntityTypeConfiguration<FeatureFlag>
    {
        public void Configure(EntityTypeBuilder<FeatureFlag> builder)
        {
            builder.ToTable("feature_flags");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            builder.Property(x => x.Key).HasMaxLength(50).IsRequired();
            builder.Property(x => x.Enabled).HasDefaultValue(false);
            builder.Property(x => x.Scope).HasMaxLength(50).HasDefaultValue("global").IsRequired();
            builder.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");

            builder.HasIndex(x => x.Key).IsUnique();
        }
    }
}
