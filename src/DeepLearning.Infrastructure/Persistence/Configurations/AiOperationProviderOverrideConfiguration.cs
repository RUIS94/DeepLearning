using DeepLearning.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeepLearning.Infrastructure.Persistence.Configurations
{
    public class AiOperationProviderOverrideConfiguration : IEntityTypeConfiguration<AiOperationProviderOverride>
    {
        public void Configure(EntityTypeBuilder<AiOperationProviderOverride> builder)
        {
            builder.ToTable("ai_operation_provider_overrides");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            builder.Property(x => x.ProviderKey).HasMaxLength(50).IsRequired();
            builder.Property(x => x.Model).HasMaxLength(100);
            builder.Property(x => x.Effort).HasMaxLength(20);
            builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
            builder.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");

            // One override per operation type — a second row for the same operation would just
            // be an ambiguous "which one wins" question with no useful answer.
            builder.HasIndex(x => x.OperationType).IsUnique();
        }
    }
}
