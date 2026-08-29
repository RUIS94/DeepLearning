using DeepLearning.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeepLearning.Infrastructure.Persistence.Configurations
{
    public class LlmProviderSettingsConfiguration : IEntityTypeConfiguration<LlmProviderSettings>
    {
        public void Configure(EntityTypeBuilder<LlmProviderSettings> builder)
        {
            builder.ToTable("llm_provider_settings");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            builder.Property(x => x.ProviderKey).HasMaxLength(50).IsRequired();
            builder.Property(x => x.IsActive).HasDefaultValue(false);
            builder.Property(x => x.ThinkingEnabled).HasDefaultValue(true);
            builder.Property(x => x.Effort).HasMaxLength(20);
            builder.Property(x => x.ExtraSettings).HasColumnType("jsonb");
            builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
            builder.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");

            builder.HasIndex(x => x.ProviderKey).IsUnique();

            // At most one active provider at a time — a plain unique index on IsActive
            // would only allow one TRUE *and* one FALSE row; the partial filter restricts
            // uniqueness to the TRUE rows only.
            builder.HasIndex(x => x.IsActive)
                .IsUnique()
                .HasDatabaseName("ux_llm_provider_settings_single_active")
                .HasFilter("is_active = true");
        }
    }
}
