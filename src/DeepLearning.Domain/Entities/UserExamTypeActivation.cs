using DeepLearning.Domain.Common;

namespace DeepLearning.Domain.Entities
{
    /// <summary>
    /// Per-user "have I added this exam type to my own practice scope" flag (U3, ref/管理员与
    /// 用户权限隔离_策划书.md Phase 4). Layers on top of, never replaces, the admin-controlled
    /// <see cref="ExamType.IsActive"/>: that flag decides whether an exam type exists/is
    /// selectable at all system-wide; this one decides whether a specific user has personally
    /// activated it. A row here with IsActive=true is only *effectively* active for that user
    /// while the exam type's own IsActive is also true — callers must check both, not just this
    /// table, so an admin disabling an exam type immediately turns it off for every user without
    /// having to touch every activation row.
    /// </summary>
    public class UserExamTypeActivation : Entity
    {
        public Guid UserId { get; set; }

        public Guid ExamTypeId { get; set; }

        public bool IsActive { get; set; }

        public DateTimeOffset ActivatedAt { get; set; }

        public User? User { get; set; }

        public ExamType? ExamType { get; set; }
    }
}
