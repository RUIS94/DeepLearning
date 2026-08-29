using DeepLearning.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeepLearning.Infrastructure.Persistence.Configurations
{
    public class LlmProviderModelConfiguration : IEntityTypeConfiguration<LlmProviderModel>
    {
        public void Configure(EntityTypeBuilder<LlmProviderModel> builder)
        {
            builder.ToTable("llm_provider_models");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            builder.Property(x => x.ProviderKey).HasMaxLength(50).IsRequired();
            builder.Property(x => x.Model).HasMaxLength(100).IsRequired();
            builder.Property(x => x.Label).HasMaxLength(100);
            builder.Property(x => x.IsCurrent).HasDefaultValue(false);
            builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");

            // Catalog entries, not a queue of "current" models — a provider can have as many
            // rows here as it has known models, but never two rows for the same exact model.
            builder.HasIndex(x => new { x.ProviderKey, x.Model }).IsUnique();

            // At most one IsCurrent=true row per provider — unlike llm_provider_settings'
            // single global IsActive row, this is scoped per ProviderKey (each provider gets
            // its own current model, independent of every other provider's).
            builder.HasIndex(x => x.ProviderKey)
                .IsUnique()
                .HasDatabaseName("ux_llm_provider_models_single_current_per_provider")
                .HasFilter("is_current = true");
        }
    }
}
