namespace DeepLearning.Domain.Exceptions
{
    public class AiCallFailedException : DomainException
    {
        public AiCallFailedException(string message, Exception? innerException = null)
            : base(message, innerException)
        {
        }
    }
}
