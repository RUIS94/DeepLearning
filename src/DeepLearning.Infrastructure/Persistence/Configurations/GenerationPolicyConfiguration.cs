using DeepLearning.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeepLearning.Infrastructure.Persistence.Configurations
{
    public class GenerationPolicyConfiguration : IEntityTypeConfiguration<GenerationPolicy>
    {
        public void Configure(EntityTypeBuilder<GenerationPolicy> builder)
        {
            builder.ToTable("generation_policy");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            builder.Property(x => x.PolicyKey).HasMaxLength(50).IsRequired();
            builder.Property(x => x.PolicyValue).HasColumnType("jsonb").IsRequired();
            builder.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");

            builder.HasIndex(x => new { x.ExamTypeId, x.PolicyKey }).IsUnique();

            builder.HasOne(x => x.ExamType)
                .WithMany()
                .HasForeignKey(x => x.ExamTypeId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
