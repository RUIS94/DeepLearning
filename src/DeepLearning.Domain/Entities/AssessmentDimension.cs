using DeepLearning.Domain.Common;
using DeepLearning.Domain.Enums;

namespace DeepLearning.Domain.Entities
{
    public class AssessmentDimension : Entity
    {
        public Guid ExamTypeId { get; set; }
        public string DimensionKey { get; set; } = string.Empty;
        public string DimensionName { get; set; } = string.Empty;
        public ScaleType ScaleType { get; set; }
        public string? PassThreshold { get; set; }
        public string LevelDescriptions { get; set; } = string.Empty;
        public string RubricVersion { get; set; } = string.Empty;
        public DateTimeOffset EffectiveFrom { get; set; }
        public DateTimeOffset? EffectiveTo { get; set; }
        public string? SourceReference { get; set; }
        public DateTimeOffset? VerifiedAt { get; set; }
        public DateTimeOffset CreatedAt { get; set; }

        /// <summary>
        /// 该评分维度仅适用于指定考试任务类型时才设置(如Task B专属维度);为空表示适用于该考试类型下的全部任务类型。
        /// </summary>
        public TaskType? ApplicableTaskType { get; set; }

        public ExamType? ExamType { get; set; }
    }
}
