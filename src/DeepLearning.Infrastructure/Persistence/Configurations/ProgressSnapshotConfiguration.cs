using DeepLearning.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeepLearning.Infrastructure.Persistence.Configurations
{
    public class ProgressSnapshotConfiguration : IEntityTypeConfiguration<ProgressSnapshot>
    {
        public void Configure(EntityTypeBuilder<ProgressSnapshot> builder)
        {
            builder.ToTable("progress_snapshots");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            builder.Property(x => x.DifficultyTier).HasMaxLength(20);
            builder.Property(x => x.AvgBandMeaningTransfer).HasPrecision(3, 1);
            builder.Property(x => x.AvgBandTextualNorms).HasPrecision(3, 1);
            builder.Property(x => x.AvgBandLanguageProficiency).HasPrecision(3, 1);
            builder.Property(x => x.PassRate).HasPrecision(5, 2);
            builder.Property(x => x.KeyTurningPoint).HasDefaultValue(false);
            builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");

            builder.HasIndex(x => new { x.UserId, x.PeriodStart }).HasDatabaseName("idx_progress_snapshots_user");

            builder.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
