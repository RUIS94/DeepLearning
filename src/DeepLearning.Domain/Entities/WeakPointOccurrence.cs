using DeepLearning.Domain.Common;

namespace DeepLearning.Domain.Entities
{
    public class WeakPointOccurrence : Entity
    {
        public Guid WeakPointId { get; set; }
        public Guid SubmissionId { get; set; }
        public Guid? ErrorListId { get; set; }
        public bool IsRecurrence { get; set; }

        /// <summary>The offending source/target text fragment for this occurrence, copied from the matched ErrorListItem, for review UIs.</summary>
        public string? Snippet { get; set; }

        /// <summary>The band assigned to the dimension this occurrence's error fell under, at the time of grading.</summary>
        public int? DetectedBand { get; set; }

        public DateTimeOffset CreatedAt { get; set; }

        public WeakPoint? WeakPoint { get; set; }
        public Submission? Submission { get; set; }
        public ErrorListItem? ErrorList { get; set; }
    }
}
