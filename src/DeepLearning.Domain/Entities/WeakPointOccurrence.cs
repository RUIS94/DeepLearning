using DeepLearning.Domain.Common;

namespace DeepLearning.Domain.Entities
{
    public class WeakPointOccurrence : Entity
    {
        public Guid WeakPointId { get; set; }
        public Guid SubmissionId { get; set; }
        public Guid? ErrorListId { get; set; }
        public bool IsRecurrence { get; set; }
        public DateTimeOffset CreatedAt { get; set; }

        public WeakPoint? WeakPoint { get; set; }
        public Submission? Submission { get; set; }
        public ErrorListItem? ErrorList { get; set; }
    }
}
