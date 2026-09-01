using MediatR;

namespace DeepLearning.Application.Features.Submissions.Queries.ListSubmissions
{
    /// <summary>
    /// A user's submissions, newest first, optionally scoped to one question — backs the
    /// "打开做过的记录" list on the question bank page.
    /// </summary>
    public record ListSubmissionsQuery(Guid UserId, Guid? QuestionId) : IRequest<List<ListSubmissionsResultItem>>;
}
