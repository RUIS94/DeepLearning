using MediatR;

namespace DeepLearning.Application.Features.ExamConfig.Commands.UpdatePromptTemplate
{
    /// <summary>
    /// Edits an existing prompt_templates row's body / version / active flag. Type/layer/exam-type
    /// scoping is treated as the row's identity and is not editable — create a new row for a
    /// different scope.
    /// </summary>
    public record UpdatePromptTemplateCommand(
        Guid Id,
        string TemplateContent,
        int Version,
        bool IsActive) : IRequest<UpdatePromptTemplateResult>;
}
