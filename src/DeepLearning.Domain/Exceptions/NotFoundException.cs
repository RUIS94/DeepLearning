namespace DeepLearning.Domain.Exceptions
{
    public class NotFoundException : DomainException
    {
        public string EntityName { get; }
        public object Key { get; }

        public NotFoundException(string entityName, object key)
            : base($"{entityName} with id '{key}' was not found.")
        {
            EntityName = entityName;
            Key = key;
        }
    }
}
