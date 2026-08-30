using DeepLearning.Api.Middleware;
using DeepLearning.Api.Services;
using DeepLearning.Application;
using DeepLearning.Application.Interfaces;
using DeepLearning.Infrastructure;
using DeepLearning.Infrastructure.BackgroundJobs;
using Hangfire;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

// Serilog.AspNetCore was already referenced — and CorrelationIdMiddleware already pushes
// CorrelationId into Serilog.Context.LogContext — but UseSerilog() was never actually called,
// so that push had no sink to reach. Enrich.FromLogContext() is what makes CorrelationId (and,
// via AiTracingHandler in Development, full AI request/response tracing — the HttpClient call
// runs inside the same async flow as the originating request) actually show up on log lines.
builder.Host.UseSerilog((context, configuration) => configuration
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Properties:j}{NewLine}{Exception}"));

// Add services to the container.

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();

// Registration/login are Supabase Auth's job entirely — this backend never issues or checks
// passwords, only validates the JWT Supabase Auth already issued. Supabase's newer projects sign
// with an asymmetric key (JWKS), which is what Authority-based discovery below is for; a legacy
// project on the shared HS256 secret would need TokenValidationParameters.IssuerSigningKey set to
// a SymmetricSecurityKey instead — see AGENTS.md's Auth section for the full design and the one
// piece (whether Supabase serves OIDC discovery at this exact path) worth confirming against a
// real token once Supabase:ProjectUrl is filled in.
var supabaseUrl = builder.Configuration["Supabase:ProjectUrl"];
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        if (string.IsNullOrEmpty(supabaseUrl))
        {
            // No project configured (e.g. a fresh clone before appsettings.Development.json is
            // filled in) — degrade to "no token will ever validate" rather than crashing at
            // startup, same fallback philosophy as LlmClientResolver's missing-config handling.
            return;
        }

        options.Authority = $"{supabaseUrl}/auth/v1";
        // Claims keep their original JWT names ("sub", "email", ...) instead of being remapped to
        // the long ClaimTypes.* URIs — CurrentUserService and EnsureUserProfileMiddleware both
        // read claims by their raw Supabase names.
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = $"{supabaseUrl}/auth/v1",
            ValidateAudience = true,
            ValidAudience = "authenticated",
            ValidateLifetime = true,
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();

    // No auth story for the dashboard itself yet (this codebase has no admin/role concept at
    // all — see AGENTS.md's Auth section), so it's Development-only for now, same gating as
    // Scalar/OpenApi above rather than exposed unauthenticated in production.
    app.UseHangfireDashboard();
}

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseMiddleware<EnsureUserProfileMiddleware>();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

// Design doc §11.2 Step 9's "Hangfire定时任务生成快照" — user's earlier decision on cadence was
// weekly (matches §10.6's own "建议每周" precedent for the calibration report Step 10 will add
// alongside this). Registering here (not inside AddInfrastructure) mirrors this codebase's own
// "Program.cs wires concrete app behavior, DependencyInjection.cs only wires services" split.
RecurringJob.AddOrUpdate<ProgressSnapshotJob>(
    "progress-snapshot-weekly",
    job => job.RunAsync(CancellationToken.None),
    Cron.Weekly);

app.Run();

public partial class Program
{
}
