using DeepLearning.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeepLearning.Infrastructure.Persistence.Configurations
{
    public class FollowUpMessageConfiguration : IEntityTypeConfiguration<FollowUpMessage>
    {
        public void Configure(EntityTypeBuilder<FollowUpMessage> builder)
        {
            builder.ToTable("follow_up_messages");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            builder.Property(x => x.Content).IsRequired();
            builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");

            builder.HasIndex(x => new { x.ThreadId, x.CreatedAt }).HasDatabaseName("idx_follow_up_messages_thread");

            builder.HasOne(x => x.Thread)
                .WithMany(x => x.Messages)
                .HasForeignKey(x => x.ThreadId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
