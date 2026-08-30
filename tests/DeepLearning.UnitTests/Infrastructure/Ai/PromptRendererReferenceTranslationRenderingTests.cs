using DeepLearning.Infrastructure.Ai;

namespace DeepLearning.UnitTests.Infrastructure.Ai
{
    /// <summary>
    /// CreateFollowUpQuestionCommandHandler.BuildTemplateModel sets ReferenceTranslation to a C#
    /// null (not an empty object) when Step 7's deep-learning content hasn't been generated for
    /// the question yet — pins down, before wiring the real followup template around it, that
    /// Scriban's {{ if reference_translation }} guard actually treats that null as falsy (and
    /// never dereferences .reference_text) rather than throwing or rendering "true" for a
    /// present-but-null property. Same "exercise the exact Scriban shape before building on top
    /// of it" precedent as PromptRendererDimensionRenderingTests/PromptRendererFollowUpTemplateRenderingTests.
    /// </summary>
    public class PromptRendererReferenceTranslationRenderingTests
    {
        private const string Template = """
            前置内容。
            {{ if reference_translation }}
            参考译文: {{ reference_translation.reference_text }}
            {{ end }}
            结束。
            """;

        [Fact]
        public void Renders_the_reference_translation_section_when_present()
        {
            var renderer = new PromptRenderer();

            var result = renderer.Render(Template, new
            {
                ReferenceTranslation = new { ReferenceText = "这是参考译文。" },
            });

            Assert.Contains("参考译文: 这是参考译文。", result);
            Assert.Contains("结束。", result);
        }

        [Fact]
        public void Omits_the_reference_translation_section_when_null()
        {
            var renderer = new PromptRenderer();

            var result = renderer.Render(Template, new
            {
                ReferenceTranslation = (object?)null,
            });

            Assert.DoesNotContain("参考译文:", result);
            Assert.Contains("前置内容。", result);
            Assert.Contains("结束。", result);
        }
    }
}
