using DeepLearning.Application.Interfaces;
using DeepLearning.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DeepLearning.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string 'DefaultConnection' was not found.");

            services.AddDbContext<AppDbContext>(options => options
                .UseNpgsql(connectionString, NpgsqlEnumConfiguration.MapEnums)
                .UseSnakeCaseNamingConvention());

            services.AddScoped<IUnitOfWork, UnitOfWork>();

            return services;
        }
    }
}
