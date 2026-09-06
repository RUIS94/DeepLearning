using DeepLearning.Application.Features.ReviewLibrary.Commands.AnalyzeVocabSemanticDrift;
using DeepLearning.Application.Interfaces;
using Hangfire;
using MediatR;
using Microsoft.Extensions.Logging;

namespace DeepLearning.Infrastructure.BackgroundJobs
{
    /// <summary>
    /// Runs vocab-glossary semantic-drift analysis off the deep-learning generation path: for
    /// each expression the just-generated question shares with an earlier one, an AI call decides
    /// whether this passage adds a sense the glossary doesn't already record.
    ///
    /// <para><b>No automatic retries.</b> It makes one LLM call for a result nobody is waiting on;
    /// the command already swallows failure (the glossary just keeps its current text), so a
    /// Hangfire retry would only spend money re-deciding the same thing. Same policy as
    /// <see cref="GenerateWeakPointsJob"/>.</para>
    /// </summary>
    [AutomaticRetry(Attempts = 0)]
    public class AnalyzeVocabSemanticDriftJob
    {
        private readonly IMediator _mediator;
        private readonly ILogger<AnalyzeVocabSemanticDriftJob> _logger;

        public AnalyzeVocabSemanticDriftJob(IMediator mediator, ILogger<AnalyzeVocabSemanticDriftJob> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        public async Task RunAsync(Guid questionId, Guid examTypeId, CancellationToken cancellationToken = default)
        {
            try
            {
                await _mediator.Send(new AnalyzeVocabSemanticDriftForQuestionCommand(questionId, examTypeId), cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Vocab semantic-drift analysis failed for question {QuestionId}.", questionId);
            }
        }
    }

    /// <summary>Production <see cref="IVocabGlossaryQueue"/>: hands the run to Hangfire.</summary>
    public class HangfireVocabGlossaryQueue : IVocabGlossaryQueue
    {
        private readonly IBackgroundJobClient _backgroundJobs;

        public HangfireVocabGlossaryQueue(IBackgroundJobClient backgroundJobs)
        {
            _backgroundJobs = backgroundJobs;
        }

        public Task EnqueueAsync(Guid questionId, Guid examTypeId, CancellationToken cancellationToken = default)
        {
            _backgroundJobs.Enqueue<AnalyzeVocabSemanticDriftJob>(
                job => job.RunAsync(questionId, examTypeId, CancellationToken.None));
            return Task.CompletedTask;
        }
    }
}
