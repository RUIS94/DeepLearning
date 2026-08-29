using DeepLearning.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeepLearning.Infrastructure.Persistence.Configurations
{
    public class KnowledgePointConfiguration : IEntityTypeConfiguration<KnowledgePoint>
    {
        public void Configure(EntityTypeBuilder<KnowledgePoint> builder)
        {
            builder.ToTable("knowledge_points");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            builder.Property(x => x.Title).HasMaxLength(255).IsRequired();
            builder.Property(x => x.Payload).HasColumnType("jsonb");
            builder.Property(x => x.Domain).HasMaxLength(50);
            builder.Property(x => x.Scenario).HasMaxLength(100);
            builder.Property(x => x.FrequencyTag).HasMaxLength(20);
            builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");

            builder.HasOne(x => x.ExamType)
                .WithMany()
                .HasForeignKey(x => x.ExamTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.Question)
                .WithMany()
                .HasForeignKey(x => x.QuestionId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}
