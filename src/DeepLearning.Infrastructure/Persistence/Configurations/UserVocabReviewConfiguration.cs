using DeepLearning.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeepLearning.Infrastructure.Persistence.Configurations
{
    public class UserVocabReviewConfiguration : IEntityTypeConfiguration<UserVocabReview>
    {
        public void Configure(EntityTypeBuilder<UserVocabReview> builder)
        {
            builder.ToTable("user_vocab_review");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            builder.Property(x => x.TimesEncountered).HasDefaultValue(1);
            builder.Property(x => x.MasteryLevel).HasDefaultValue(Domain.Enums.MasteryLevel.New).ValueGeneratedNever();
            builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");

            builder.HasIndex(x => new { x.UserId, x.VocabId }).IsUnique();

            builder.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.Vocab)
                .WithMany()
                .HasForeignKey(x => x.VocabId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
