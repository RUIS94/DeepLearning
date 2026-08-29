using DeepLearning.Domain.Enums;

namespace DeepLearning.Application.Interfaces
{
    /// <summary>
    /// Pulls the applicable prompt_templates rows for an exam type + operation
    /// (shared_methodology layer by subject category, exam_specific layer by exam type id)
    /// and renders them through Scriban with the given model, shared-then-specific.
    /// </summary>
    public interface IExamConfigLoader
    {
        Task<string> BuildPromptAsync(
            Guid examTypeId,
            AiOperationType templateType,
            object templateModel,
            CancellationToken cancellationToken = default);
    }
}
