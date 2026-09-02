using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Exceptions;
using MediatR;

namespace DeepLearning.Application.Features.Submissions.Queries.GetSubmissionById
{
    public class GetSubmissionByIdQueryHandler : IRequestHandler<GetSubmissionByIdQuery, GetSubmissionByIdResult>
    {
        private readonly ISubmissionRepository _submissionRepository;
        private readonly IGradingSummaryRepository _gradingSummaryRepository;

        public GetSubmissionByIdQueryHandler(
            ISubmissionRepository submissionRepository,
            IGradingSummaryRepository gradingSummaryRepository)
        {
            _submissionRepository = submissionRepository;
            _gradingSummaryRepository = gradingSummaryRepository;
        }

        public async Task<GetSubmissionByIdResult> Handle(GetSubmissionByIdQuery request, CancellationToken cancellationToken)
        {
            var submission = await _submissionRepository.GetByIdAsync(request.Id, cancellationToken)
                ?? throw new NotFoundException(nameof(Submission), request.Id);

            var gradingResults = await _submissionRepository.GetGradingResultsAsync(request.Id, cancellationToken);
            var errorList = await _submissionRepository.GetErrorListAsync(request.Id, cancellationToken);
            var overallSummary = await _gradingSummaryRepository.GetBySubmissionIdAsync(request.Id, cancellationToken);

            return new GetSubmissionByIdResult(
                submission.Id,
                submission.QuestionId,
                submission.UserId,
                submission.TaskType,
                submission.Content,
                submission.Status,
                submission.SubmittedAt,
                submission.CreatedAt,
                gradingResults.Select(r => new GradingResultItem(
                    r.Id,
                    r.Dimension!.DimensionKey,
                    r.Dimension.DimensionName,
                    r.RubricVersion,
                    r.Band,
                    r.PassBool,
                    r.Rationale,
                    r.CumulativeDensityFlag,
                    r.CumulativeDensityNote,
                    r.EstimatedPassProbability)).ToList(),
                errorList.Select(e => new ErrorListResultItem(
                    e.Id,
                    e.PositionRef,
                    e.SourceTextSnippet,
                    e.UserTextSnippet,
                    e.ErrorTaxonomy!.CategoryKey,
                    e.Dimension!.DimensionKey,
                    e.ImpactsCore,
                    e.Explanation,
                    e.Suggestion)).ToList(),
                overallSummary is null
                    ? null
                    : new GradingSummaryResult(
                        overallSummary.OverallPassProbability,
                        overallSummary.OverallPassBool,
                        overallSummary.CumulativeDensityFlag,
                        overallSummary.CumulativeDensityNote,
                        overallSummary.ConclusionText));
        }
    }
}
