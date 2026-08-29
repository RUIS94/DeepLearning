using DeepLearning.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeepLearning.Infrastructure.Persistence.Configurations
{
    public class ExamTypeConfiguration : IEntityTypeConfiguration<ExamType>
    {
        public void Configure(EntityTypeBuilder<ExamType> builder)
        {
            builder.ToTable("exam_types");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            builder.Property(x => x.Code).HasMaxLength(50).IsRequired();
            builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
            builder.Property(x => x.SourceLanguage).HasMaxLength(20);
            builder.Property(x => x.TargetLanguage).HasMaxLength(20);
            builder.Property(x => x.GradeLevel).HasMaxLength(50);
            builder.Property(x => x.IsActive).HasDefaultValue(true);
            builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");

            builder.HasIndex(x => x.Code).IsUnique();
        }
    }
}
