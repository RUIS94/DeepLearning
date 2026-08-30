using DeepLearning.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeepLearning.Infrastructure.Persistence.Configurations
{
    public class AiCallLogConfiguration : IEntityTypeConfiguration<AiCallLog>
    {
        public void Configure(EntityTypeBuilder<AiCallLog> builder)
        {
            builder.ToTable("ai_call_logs");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            builder.Property(x => x.Status).HasDefaultValue(Domain.Enums.CallStatus.pending).ValueGeneratedNever();
            builder.Property(x => x.AttemptCount).HasDefaultValue(0);
            builder.Property(x => x.MaxRetries).HasDefaultValue(3);
            builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");

            builder.HasIndex(x => x.Status).HasDatabaseName("idx_ai_call_logs_status");
        }
    }
}
