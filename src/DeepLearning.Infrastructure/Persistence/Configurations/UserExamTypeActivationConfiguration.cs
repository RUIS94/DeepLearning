using DeepLearning.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeepLearning.Infrastructure.Persistence.Configurations
{
    public class UserExamTypeActivationConfiguration : IEntityTypeConfiguration<UserExamTypeActivation>
    {
        public void Configure(EntityTypeBuilder<UserExamTypeActivation> builder)
        {
            builder.ToTable("user_exam_type_activations");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            builder.Property(x => x.IsActive).IsRequired();
            builder.Property(x => x.ActivatedAt).HasDefaultValueSql("now()");

            builder.HasIndex(x => new { x.UserId, x.ExamTypeId }).IsUnique();

            builder.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(x => x.ExamType)
                .WithMany()
                .HasForeignKey(x => x.ExamTypeId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
