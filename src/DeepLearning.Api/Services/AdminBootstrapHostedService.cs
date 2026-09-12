using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Enums;

namespace DeepLearning.Api.Services
{
    /// <summary>
    /// Idempotent "world's first admin" bootstrap: on every startup, promotes whichever
    /// `public.users` rows match `Admin:BootstrapEmails` to UserRole.admin. Exists only to get an
    /// admin account into a system that otherwise has no admin at all yet — once at least one
    /// admin exists, ongoing role management is expected to move to the admin-facing user
    /// management endpoints (Phase 2 / A3) instead of editing this config. A user who hasn't
    /// signed in yet (no `public.users` row) is silently skipped; the next startup after their
    /// first login will pick them up.
    /// </summary>
    public class AdminBootstrapHostedService : IHostedService
    {
        private readonly IServiceProvider _services;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AdminBootstrapHostedService> _logger;

        public AdminBootstrapHostedService(
            IServiceProvider services,
            IConfiguration configuration,
            ILogger<AdminBootstrapHostedService> logger)
        {
            _services = services;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            var emails = _configuration.GetSection("Admin:BootstrapEmails").Get<string[]>() ?? [];
            if (emails.Length == 0)
            {
                return;
            }

            using var scope = _services.CreateScope();
            var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var changed = false;
            foreach (var email in emails)
            {
                var user = await userRepository.GetByEmailAsync(email, cancellationToken);
                if (user is null)
                {
                    _logger.LogInformation("Admin bootstrap: {Email} has no users row yet, skipping until first login.", email);
                    continue;
                }

                if (user.Role != UserRole.admin)
                {
                    user.Role = UserRole.admin;
                    changed = true;
                    _logger.LogInformation("Admin bootstrap: promoted {Email} to admin.", email);
                }
            }

            if (changed)
            {
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
