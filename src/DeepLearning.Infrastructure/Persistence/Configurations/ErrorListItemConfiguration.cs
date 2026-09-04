using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeepLearning.Infrastructure.Persistence.Configurations
{
    public class ErrorListItemConfiguration : IEntityTypeConfiguration<ErrorListItem>
    {
        public void Configure(EntityTypeBuilder<ErrorListItem> builder)
        {
            builder.ToTable("error_list");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            builder.Property(x => x.PositionRef).HasMaxLength(100);
            builder.Property(x => x.Severity).HasDefaultValue(ErrorSeverity.minor);
            builder.Property(x => x.Summary).HasMaxLength(60);
            builder.Property(x => x.ImpactsCore).HasDefaultValue(false);
            builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");

            builder.HasIndex(x => x.SubmissionId).HasDatabaseName("idx_error_list_submission");

            builder.HasOne(x => x.Submission)
                .WithMany()
                .HasForeignKey(x => x.SubmissionId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(x => x.ErrorTaxonomy)
                .WithMany()
                .HasForeignKey(x => x.ErrorTaxonomyId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.Dimension)
                .WithMany()
                .HasForeignKey(x => x.DimensionId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
