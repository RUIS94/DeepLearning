using DeepLearning.UnitTests.TestInfrastructure;

namespace DeepLearning.UnitTests.Api
{
    [CollectionDefinition(Name)]
    public class ApiCollection : ICollectionFixture<ApiWebApplicationFactory>
    {
        public const string Name = "Api";
    }
}
