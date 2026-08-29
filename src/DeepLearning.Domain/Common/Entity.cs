namespace DeepLearning.Domain.Common
{
    public abstract class Entity
    {
        public Guid Id { get; set; }

        public override bool Equals(object? obj)
        {
            if (obj is not Entity other || other.GetType() != GetType())
                return false;

            return Id == other.Id;
        }

        public override int GetHashCode() => HashCode.Combine(GetType(), Id);
    }
}
