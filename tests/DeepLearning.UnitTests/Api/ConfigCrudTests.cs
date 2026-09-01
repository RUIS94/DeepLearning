using System.Net;
using System.Net.Http.Json;
using DeepLearning.Api.Constants;
using DeepLearning.Application.Features.ExamConfig.Commands.CreatePromptTemplate;
using DeepLearning.Application.Features.ExamConfig.Commands.UpdatePromptTemplate;
using DeepLearning.Application.Features.ExamConfig.Queries.GetPromptTemplatesByExamType;
using DeepLearning.Application.Features.Questions.Commands.ImportUserQuestion;
using DeepLearning.Application.Features.QuestionBank.Commands.CreateQuestionBankCategory;
using DeepLearning.Application.Features.QuestionBank.Commands.UpdateQuestionBankCategory;
using DeepLearning.Application.Features.QuestionBank.Queries.ListQuestionBankCategories;
using DeepLearning.Application.Features.StandardOverrides.Commands.DeprecateStandardOverride;
using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Enums;
using DeepLearning.Infrastructure.Persistence;
using DeepLearning.UnitTests.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace DeepLearning.UnitTests.Api
{
    /// <summary>P3: edit/delete for question-bank categories & prompt templates; deprecate for standard overrides.</summary>
    [Collection(ApiCollection.Name)]
    public class ConfigCrudTests
    {
        private readonly ApiWebApplicationFactory _factory;

        public ConfigCrudTests(ApiWebApplicationFactory factory)
        {
            _factory = factory;
        }

        // ---- Question bank categories ----------------------------------------------------------

        [Fact]
        public async Task Category_update_changes_name_and_description()
        {
            var client = _factory.CreateClient();
            var created = await client.PostAsJsonAsync(ApiRoutes.QuestionBankCategories.Base, new
            {
                CategoryType = CategoryType.domain,
                Name = $"cat {Guid.NewGuid():N}",
                ParentId = (Guid?)null,
                Description = (string?)null,
            });
            var id = (await created.Content.ReadFromJsonAsync<CreateQuestionBankCategoryResult>())!.Id;

            var put = await client.PutAsJsonAsync($"{ApiRoutes.QuestionBankCategories.Base}/{id}", new
            {
                Name = "renamed",
                ParentId = (Guid?)null,
                Description = "now with a description",
            });
            Assert.Equal(HttpStatusCode.OK, put.StatusCode);
            var updated = await put.Content.ReadFromJsonAsync<UpdateQuestionBankCategoryResult>();
            Assert.Equal("renamed", updated!.Name);
            Assert.Equal("now with a description", updated.Description);

            var list = await client.GetFromJsonAsync<List<ListQuestionBankCategoriesResultItem>>(
                ApiRoutes.QuestionBankCategories.Base);
            var row = Assert.Single(list!, c => c.Id == id);
            Assert.Equal("renamed", row.Name);
            Assert.Equal("now with a description", row.Description);
        }

        [Fact]
        public async Task Category_delete_is_blocked_while_it_has_children_or_tagged_questions_then_succeeds_when_clean()
        {
            var client = _factory.CreateClient();

            var parentResp = await client.PostAsJsonAsync(ApiRoutes.QuestionBankCategories.Base, new
            { CategoryType = CategoryType.domain, Name = $"parent {Guid.NewGuid():N}", ParentId = (Guid?)null, Description = (string?)null });
            var parentId = (await parentResp.Content.ReadFromJsonAsync<CreateQuestionBankCategoryResult>())!.Id;

            var childResp = await client.PostAsJsonAsync(ApiRoutes.QuestionBankCategories.Base, new
            { CategoryType = CategoryType.domain, Name = $"child {Guid.NewGuid():N}", ParentId = parentId, Description = (string?)null });
            var childId = (await childResp.Content.ReadFromJsonAsync<CreateQuestionBankCategoryResult>())!.Id;

            // parent has a child -> 409
            Assert.Equal(HttpStatusCode.Conflict,
                (await client.DeleteAsync($"{ApiRoutes.QuestionBankCategories.Base}/{parentId}")).StatusCode);

            // tag a question onto the child -> child now referenced -> 409
            var questionResp = await client.PostAsJsonAsync(ApiRoutes.Questions.Base, new
            {
                TaskType = TaskType.A,
                Difficulty = Difficulty.medium,
                Title = $"q {Guid.NewGuid():N}",
                Brief = (string?)null,
                SourceText = "text",
                FlawedTranslationText = (string?)null,
                WordCount = 1,
                CreatedBy = (Guid?)null,
                Visibility = Visibility.Private,
                MeaningCheckpoints = Array.Empty<object>(),
                SeededErrors = Array.Empty<object>(),
            });
            var questionId = (await questionResp.Content.ReadFromJsonAsync<ImportUserQuestionResult>())!.Id;
            await client.PostAsync(
                $"{ApiRoutes.QuestionBankCategories.Base}/{childId}/questions/{questionId}", null);
            Assert.Equal(HttpStatusCode.Conflict,
                (await client.DeleteAsync($"{ApiRoutes.QuestionBankCategories.Base}/{childId}")).StatusCode);

            // a fresh unused category -> 204
            var loneResp = await client.PostAsJsonAsync(ApiRoutes.QuestionBankCategories.Base, new
            { CategoryType = CategoryType.scenario, Name = $"lone {Guid.NewGuid():N}", ParentId = (Guid?)null, Description = (string?)null });
            var loneId = (await loneResp.Content.ReadFromJsonAsync<CreateQuestionBankCategoryResult>())!.Id;
            Assert.Equal(HttpStatusCode.NoContent,
                (await client.DeleteAsync($"{ApiRoutes.QuestionBankCategories.Base}/{loneId}")).StatusCode);
        }

        // ---- Prompt templates -----------------------------------------------------------------

        [Fact]
        public async Task Prompt_template_update_and_delete_and_inactive_filter()
        {
            var client = _factory.CreateClient();
            var created = await client.PostAsJsonAsync(ApiRoutes.PromptTemplates.Base, new
            {
                ExamTypeId = (Guid?)null,
                SubjectCategory = SubjectCategory.translation,
                TemplateType = AiOperationType.question_gen,
                Layer = TemplateLayer.shared_methodology,
                TemplateContent = "v1 body",
                Version = 1,
            });
            var id = (await created.Content.ReadFromJsonAsync<CreatePromptTemplateResult>())!.Id;

            var put = await client.PutAsJsonAsync($"{ApiRoutes.PromptTemplates.Base}/{id}", new
            {
                TemplateContent = "v2 body",
                Version = 2,
                IsActive = false,
            });
            Assert.Equal(HttpStatusCode.OK, put.StatusCode);
            var updated = await put.Content.ReadFromJsonAsync<UpdatePromptTemplateResult>();
            Assert.Equal("v2 body", updated!.TemplateContent);
            Assert.False(updated.IsActive);

            // default list (active only) excludes it; ?isActive=false includes it
            var activeOnly = await client.GetFromJsonAsync<List<PromptTemplateResultItem>>(
                $"{ApiRoutes.PromptTemplates.Base}?isActive=true");
            Assert.DoesNotContain(activeOnly!, t => t.Id == id);
            var inactive = await client.GetFromJsonAsync<List<PromptTemplateResultItem>>(
                $"{ApiRoutes.PromptTemplates.Base}?isActive=false");
            Assert.Contains(inactive!, t => t.Id == id);

            Assert.Equal(HttpStatusCode.NoContent,
                (await client.DeleteAsync($"{ApiRoutes.PromptTemplates.Base}/{id}")).StatusCode);
            var all = await client.GetFromJsonAsync<List<PromptTemplateResultItem>>(ApiRoutes.PromptTemplates.Base);
            Assert.DoesNotContain(all!, t => t.Id == id);
        }

        // ---- Standard overrides -------------------------------------------------------------

        [Fact]
        public async Task Standard_override_deprecate_then_conflict_on_second_call()
        {
            var id = Guid.NewGuid();
            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                context.StandardOverrides.Add(new StandardOverride
                {
                    Id = id,
                    Scope = OverrideScope.grading_rubric,
                    DimensionOrRule = $"rule-{id:N}",
                    RevisedRuleText = "next time, conclude X",
                    Status = OverrideStatus.observing,
                    CreatedAt = DateTimeOffset.UtcNow,
                });
                await context.SaveChangesAsync();
            }

            var client = _factory.CreateClient();
            var first = await client.PostAsync($"{ApiRoutes.StandardOverrides.Base}/{id}/deprecate", null);
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
            var result = await first.Content.ReadFromJsonAsync<DeprecateStandardOverrideResult>();
            Assert.Equal(OverrideStatus.deprecated, result!.Status);

            var second = await client.PostAsync($"{ApiRoutes.StandardOverrides.Base}/{id}/deprecate", null);
            Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        }
    }
}
