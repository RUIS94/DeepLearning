namespace DeepLearning.Domain.Exceptions
{
    /// <summary>Thrown when a user tries to activate an exam type the admin has globally disabled (400).</summary>
    public class ExamTypeInactiveException : DomainException
    {
        public ExamTypeInactiveException(Guid examTypeId)
            : base($"Exam type '{examTypeId}' is not currently active and cannot be activated.")
        {
        }
    }
}
