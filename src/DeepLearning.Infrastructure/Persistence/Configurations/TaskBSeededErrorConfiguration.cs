using DeepLearning.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeepLearning.Infrastructure.Persistence.Configurations
{
    public class TaskBSeededErrorConfiguration : IEntityTypeConfiguration<TaskBSeededError>
    {
        public void Configure(EntityTypeBuilder<TaskBSeededError> builder)
        {
            builder.ToTable("task_b_seeded_errors");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            builder.Property(x => x.CorrectReferenceText).IsRequired();
            builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");

            builder.HasOne(x => x.Question)
                .WithMany()
                .HasForeignKey(x => x.QuestionId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(x => x.ErrorTaxonomy)
                .WithMany()
                .HasForeignKey(x => x.ErrorTaxonomyId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
