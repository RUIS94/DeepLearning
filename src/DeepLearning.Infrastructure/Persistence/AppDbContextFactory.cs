using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DeepLearning.Infrastructure.Persistence
{
    /// <summary>
    /// 仅供 `dotnet ef migrations` 命令行工具在设计期使用,不参与运行时依赖注入。
    /// 运行时连接字符串由 DependencyInjection.AddInfrastructure 从应用配置读取。
    /// </summary>
    public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
                ?? "Host=localhost;Port=5432;Database=deeplearning;Username=postgres;Password=postgres";

            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(connectionString, NpgsqlEnumConfiguration.MapEnums)
                .UseSnakeCaseNamingConvention();

            return new AppDbContext(optionsBuilder.Options);
        }
    }
}
