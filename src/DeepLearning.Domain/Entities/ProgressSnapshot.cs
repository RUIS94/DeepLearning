using DeepLearning.Domain.Common;

namespace DeepLearning.Domain.Entities
{
    public class ProgressSnapshot : Entity
    {
        public Guid UserId { get; set; }
        public DateOnly PeriodStart { get; set; }
        public DateOnly PeriodEnd { get; set; }
        public string? DifficultyTier { get; set; }
        public decimal? AvgBandMeaningTransfer { get; set; }
        public decimal? AvgBandTextualNorms { get; set; }
        public decimal? AvgBandLanguageProficiency { get; set; }
        public decimal? PassRate { get; set; }
        public string? TrendNote { get; set; }
        public bool KeyTurningPoint { get; set; }
        public DateTimeOffset CreatedAt { get; set; }

        public User? User { get; set; }
    }
}
