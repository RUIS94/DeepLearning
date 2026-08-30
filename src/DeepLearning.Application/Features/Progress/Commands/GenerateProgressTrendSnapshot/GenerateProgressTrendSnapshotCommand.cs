using MediatR;

namespace DeepLearning.Application.Features.Progress.Commands.GenerateProgressTrendSnapshot
{
    /// <summary>
    /// One (user, difficulty tier, week) unit of ProgressSnapshotJob's work — the job itself just
    /// enumerates active users x the 3 Difficulty tiers x the trailing weeks and sends one of
    /// these per combination via IMediator, so the actual recompute/AI-interpretation logic lives
    /// here as an ordinary, directly-testable CQRS handler rather than inside the Hangfire job
    /// class (matches AGENTS.md's "Controllers/jobs only call IMediator.Send, no business logic"
    /// convention, extended from HTTP controllers to the one other entry point this codebase has).
    /// </summary>
    public record GenerateProgressTrendSnapshotCommand(
        Guid UserId,
        Guid ExamTypeId,
        string DifficultyTier,
        DateOnly PeriodStart,
        DateOnly PeriodEnd) : IRequest<GenerateProgressTrendSnapshotResult>;
}
