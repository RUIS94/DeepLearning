using DeepLearning.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeepLearning.Infrastructure.Persistence.Configurations
{
    public class UserFeatureOverrideConfiguration : IEntityTypeConfiguration<UserFeatureOverride>
    {
        public void Configure(EntityTypeBuilder<UserFeatureOverride> builder)
        {
            builder.ToTable("user_feature_overrides");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            builder.Property(x => x.FeatureKey).HasMaxLength(50).IsRequired();
            builder.Property(x => x.Enabled).IsRequired();
            builder.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");

            builder.HasIndex(x => new { x.UserId, x.FeatureKey }).IsUnique();

            builder.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
