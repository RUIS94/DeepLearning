using DeepLearning.Application.Interfaces;
using MediatR;

namespace DeepLearning.Application.Features.Questions.Queries.ListQuestions
{
    public class ListQuestionsQueryHandler : IRequestHandler<ListQuestionsQuery, List<ListQuestionsResultItem>>
    {
        private readonly IQuestionRepository _questionRepository;

        public ListQuestionsQueryHandler(IQuestionRepository questionRepository)
        {
            _questionRepository = questionRepository;
        }

        public async Task<List<ListQuestionsResultItem>> Handle(ListQuestionsQuery request, CancellationToken cancellationToken)
        {
            var questions = await _questionRepository.ListAsync(request.TaskType, request.Difficulty, request.InBank, request.CategoryId, cancellationToken);

            return questions.Select(x => new ListQuestionsResultItem(
                x.Id, x.TaskType, x.Difficulty, x.Title, x.WordCount, x.InBank, x.CreatedAt)).ToList();
        }
    }
}
