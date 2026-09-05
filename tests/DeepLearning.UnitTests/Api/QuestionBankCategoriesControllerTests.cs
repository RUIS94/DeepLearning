using System.Net;
using System.Net.Http.Json;
using DeepLearning.Api.Constants;
using DeepLearning.Application.Features.ExamConfig.Commands.CreateExamType;
using DeepLearning.Application.Features.Questions.Commands.GenerateQuestion;
using DeepLearning.Application.Features.QuestionBank.Commands.CreateQuestionBankCategory;
using DeepLearning.Application.Features.QuestionBank.Commands.TagQuestionWithCategory;
using DeepLearning.Application.Features.QuestionBank.Queries.GetQuestionBankCategoryById;
using DeepLearning.Application.Features.QuestionBank.Queries.ListQuestionBankCategories;
using DeepLearning.Application.Features.Questions.Queries.GetQuestionById;
using DeepLearning.Application.Features.Questions.Queries.ListQuestions;
using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Enums;
using DeepLearning.UnitTests.TestInfrastructure;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace DeepLearning.UnitTests.Api
{
    [Collection(ApiCollection.Name)]
    public class QuestionBankCategoriesControllerTests
    {
        private readonly ApiWebApplicationFactory _factory;

        public QuestionBankCategoriesControllerTests(ApiWebApplicationFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task Create_then_get_by_id_round_trips_over_http()
        {
            var client = _factory.CreateClient();
            var request = new { CategoryType = CategoryType.domain, Name = $"legal_{Guid.NewGuid():N}", ParentId = (Guid?)null, Description = (string?)null };

            var createResponse = await client.PostAsJsonAsync(ApiRoutes.QuestionBankCategories.Base, request);
            Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
            var created = await createResponse.Content.ReadFromJsonAsync<CreateQuestionBankCategoryResult>();

            var getResponse = await client.GetAsync($"{ApiRoutes.QuestionBankCategories.Base}/{created!.Id}");
            Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
            var fetched = await getResponse.Content.ReadFromJsonAsync<GetQuestionBankCategoryByIdResult>();
            Assert.Equal(request.Name, fetched!.Name);
            Assert.Equal(CategoryType.domain, fetched.CategoryType);
        }

        [Fact]
        public async Task Get_by_id_returns_404_for_unknown_id()
        {
            var client = _factory.CreateClient();

            var response = await client.GetAsync($"{ApiRoutes.QuestionBankCategories.Base}/{Guid.NewGuid()}");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task Create_returns_404_when_parent_id_does_not_exist()
        {
            var client = _factory.CreateClient();
            var request = new { CategoryType = CategoryType.scenario, Name = $"policy_{Guid.NewGuid():N}", ParentId = (Guid?)Guid.NewGuid(), Description = (string?)null };

            var response = await client.PostAsJsonAsync(ApiRoutes.QuestionBankCategories.Base, request);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task Create_a_child_category_under_a_real_parent_succeeds_and_list_filters_by_category_type()
        {
            var client = _factory.CreateClient();
            var parentResponse = await client.PostAsJsonAsync(
                ApiRoutes.QuestionBankCategories.Base,
                new { CategoryType = CategoryType.domain, Name = $"medical_{Guid.NewGuid():N}", ParentId = (Guid?)null, Description = (string?)null });
            var parent = await parentResponse.Content.ReadFromJsonAsync<CreateQuestionBankCategoryResult>();

            var childResponse = await client.PostAsJsonAsync(
                ApiRoutes.QuestionBankCategories.Base,
                new { CategoryType = CategoryType.domain, Name = $"oncology_{Guid.NewGuid():N}", ParentId = (Guid?)parent!.Id, Description = (string?)null });
            Assert.Equal(HttpStatusCode.Created, childResponse.StatusCode);
            var child = await childResponse.Content.ReadFromJsonAsync<CreateQuestionBankCategoryResult>();
            Assert.Equal(parent.Id, child!.ParentId);

            var listResponse = await client.GetAsync($"{ApiRoutes.QuestionBankCategories.Base}?categoryType={CategoryType.domain}");
            Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
            var list = await listResponse.Content.ReadFromJsonAsync<List<ListQuestionBankCategoriesResultItem>>();
            Assert.Contains(list!, x => x.Id == child.Id);
        }

        /// <summary>
        /// Design doc §2.1 node C1/D1 -> CAT: tagging a question with a category is the
        /// "归入题库" action — proves it flips InBank to true and that the question then shows
        /// up when the question list is filtered by that category (FE9 "题库浏览与筛选").
        /// </summary>
        [Fact]
        public async Task Tagging_a_question_marks_it_in_bank_and_it_shows_up_when_listing_by_category()
        {
            var client = _factory
                .WithWebHostBuilder(builder => builder.ConfigureTestServices(
                    services => services.AddScoped<ILlmClientResolver>(_ => LlmClientResolverSubstitute.Returning(new FakeLlmClient()))))
                .CreateClient();

            var examTypeResponse = await client.PostAsJsonAsync(ApiRoutes.ExamTypes.Base, new
            {
                Code = $"test_{Guid.NewGuid():N}",
                Name = "API Test Exam Type",
                SubjectCategory = SubjectCategory.translation,
            });
            var examType = await examTypeResponse.Content.ReadFromJsonAsync<CreateExamTypeResult>();

            var generateResponse = await client.PostAsJsonAsync(
                $"{ApiRoutes.Questions.Base}/generate",
                new { ExamTypeId = examType!.Id, TaskType = TaskType.A, Difficulty = Difficulty.medium, CreatedBy = (Guid?)null });
            var question = await generateResponse.Content.ReadFromJsonAsync<GenerateQuestionResult>();

            var categoryResponse = await client.PostAsJsonAsync(
                ApiRoutes.QuestionBankCategories.Base,
                new { CategoryType = CategoryType.domain, Name = $"government_{Guid.NewGuid():N}", ParentId = (Guid?)null, Description = (string?)null });
            var category = await categoryResponse.Content.ReadFromJsonAsync<CreateQuestionBankCategoryResult>();

            var tagResponse = await client.PostAsync($"{ApiRoutes.QuestionBankCategories.Base}/{category!.Id}/questions/{question!.Id}", null);
            Assert.Equal(HttpStatusCode.OK, tagResponse.StatusCode);
            var tagged = await tagResponse.Content.ReadFromJsonAsync<TagQuestionWithCategoryResult>();
            Assert.True(tagged!.InBank);

            var getResponse = await client.GetAsync($"{ApiRoutes.Questions.Base}/{question.Id}");
            var fetchedQuestion = await getResponse.Content.ReadFromJsonAsync<GetQuestionByIdResult>();
            Assert.True(fetchedQuestion!.InBank);
            Assert.Contains(category.Id, fetchedQuestion.CategoryIds);

            var listResponse = await client.GetAsync($"{ApiRoutes.Questions.Base}?categoryId={category.Id}");
            var list = await listResponse.Content.ReadFromJsonAsync<List<ListQuestionsResultItem>>();
            Assert.Contains(list!, x => x.Id == question.Id);
        }

        [Fact]
        public async Task Tagging_the_same_question_with_the_same_category_twice_returns_409()
        {
            var client = _factory
                .WithWebHostBuilder(builder => builder.ConfigureTestServices(
                    services => services.AddScoped<ILlmClientResolver>(_ => LlmClientResolverSubstitute.Returning(new FakeLlmClient()))))
                .CreateClient();

            var examTypeResponse = await client.PostAsJsonAsync(ApiRoutes.ExamTypes.Base, new
            {
                Code = $"test_{Guid.NewGuid():N}",
                Name = "API Test Exam Type",
                SubjectCategory = SubjectCategory.translation,
            });
            var examType = await examTypeResponse.Content.ReadFromJsonAsync<CreateExamTypeResult>();

            var generateResponse = await client.PostAsJsonAsync(
                $"{ApiRoutes.Questions.Base}/generate",
                new { ExamTypeId = examType!.Id, TaskType = TaskType.A, Difficulty = Difficulty.medium, CreatedBy = (Guid?)null });
            var question = await generateResponse.Content.ReadFromJsonAsync<GenerateQuestionResult>();

            var categoryResponse = await client.PostAsJsonAsync(
                ApiRoutes.QuestionBankCategories.Base,
                new { CategoryType = CategoryType.domain, Name = $"finance_{Guid.NewGuid():N}", ParentId = (Guid?)null, Description = (string?)null });
            var category = await categoryResponse.Content.ReadFromJsonAsync<CreateQuestionBankCategoryResult>();

            var first = await client.PostAsync($"{ApiRoutes.QuestionBankCategories.Base}/{category!.Id}/questions/{question!.Id}", null);
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);

            var second = await client.PostAsync($"{ApiRoutes.QuestionBankCategories.Base}/{category.Id}/questions/{question.Id}", null);
            Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        }
    }
}
