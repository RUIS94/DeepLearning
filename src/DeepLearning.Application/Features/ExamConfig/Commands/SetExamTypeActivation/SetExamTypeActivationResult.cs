namespace DeepLearning.Application.Features.ExamConfig.Commands.SetExamTypeActivation
{
    public record SetExamTypeActivationResult(Guid ExamTypeId, bool IsActive);
}
