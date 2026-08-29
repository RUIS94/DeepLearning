using DeepLearning.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeepLearning.Infrastructure.Persistence.Configurations
{
    public class PromptTemplateConfiguration : IEntityTypeConfiguration<PromptTemplate>
    {
        public void Configure(EntityTypeBuilder<PromptTemplate> builder)
        {
            builder.ToTable("prompt_templates", t => t.HasCheckConstraint(
                "ck_prompt_templates_layer_scope",
                "(layer = 'exam_specific' AND exam_type_id IS NOT NULL) OR " +
                "(layer = 'shared_methodology' AND subject_category IS NOT NULL)"));
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            builder.Property(x => x.TemplateContent).IsRequired();
            builder.Property(x => x.Version).HasDefaultValue(1);
            builder.Property(x => x.IsActive).HasDefaultValue(true);
            builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");

            builder.HasOne(x => x.ExamType)
                .WithMany()
                .HasForeignKey(x => x.ExamTypeId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
