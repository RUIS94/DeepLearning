using DeepLearning.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeepLearning.Infrastructure.Persistence.Configurations
{
    public class MeaningCheckpointConfiguration : IEntityTypeConfiguration<MeaningCheckpoint>
    {
        public void Configure(EntityTypeBuilder<MeaningCheckpoint> builder)
        {
            builder.ToTable("meaning_checkpoints");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            builder.Property(x => x.CheckpointText).IsRequired();
            builder.Property(x => x.CheckpointType).HasMaxLength(50);
            builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");

            builder.HasOne(x => x.Question)
                .WithMany()
                .HasForeignKey(x => x.QuestionId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
