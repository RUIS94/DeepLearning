using System.Collections.Concurrent;
using DeepLearning.Application.Interfaces;

namespace DeepLearning.UnitTests.TestInfrastructure
{
    /// <summary>
    /// Records what deep-learning generation would have queued for vocab-glossary drift analysis,
    /// without running it — the drift command has its own handler test with a fake AI service, so
    /// an API test only needs to see whether the enqueue happened.
    /// </summary>
    public class RecordingVocabGlossaryQueue : IVocabGlossaryQueue
    {
        public ConcurrentQueue<(Guid QuestionId, Guid ExamTypeId)> Enqueued { get; } = new();

        public Task EnqueueAsync(Guid questionId, Guid examTypeId, CancellationToken cancellationToken = default)
        {
            Enqueued.Enqueue((questionId, examTypeId));
            return Task.CompletedTask;
        }
    }
}
