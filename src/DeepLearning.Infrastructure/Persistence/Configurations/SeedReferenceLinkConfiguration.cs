using DeepLearning.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeepLearning.Infrastructure.Persistence.Configurations
{
    public class SeedReferenceLinkConfiguration : IEntityTypeConfiguration<SeedReferenceLink>
    {
        public void Configure(EntityTypeBuilder<SeedReferenceLink> builder)
        {
            builder.ToTable("seed_reference_links");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");

            builder.HasOne(x => x.GeneratedQuestion)
                .WithMany()
                .HasForeignKey(x => x.GeneratedQuestionId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(x => x.SeedQuestion)
                .WithMany()
                .HasForeignKey(x => x.SeedQuestionId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
