using DeepLearning.Infrastructure.Ai;

namespace DeepLearning.UnitTests.Infrastructure.Ai
{
    /// <summary>
    /// The seeded grading prompt_templates row (seed_naati_ct_en_zh.sql) iterates
    /// `{{ for dim in dimensions }} ... {{ for band in dim.level_descriptions }} Band {{ band.key }}: {{ band.value }} {{ end }} {{ end }}`
    /// and `{{ for cat in error_taxonomies }} {{ cat.category_key }} {{ end }}` — a shape nothing
    /// in the codebase had ever rendered before GradeSubmission (grading didn't exist yet), so
    /// this pins down that Scriban really does expose Dictionary&lt;string,string&gt; entries as
    /// objects with .key/.value, and lists of anonymous objects as iterable with member access,
    /// through the exact same PromptRenderer.Render(model) call GradeSubmissionCommandHandler uses.
    /// </summary>
    public class PromptRendererDimensionRenderingTests
    {
        [Fact]
        public void Renders_a_list_of_dimensions_each_with_a_nested_level_descriptions_dictionary()
        {
            var renderer = new PromptRenderer();
            var template = "{{ for dim in dimensions }}### {{ dim.dimension_name }} ({{ dim.pass_threshold }})\n{{ for band in dim.level_descriptions }}Band {{ band.key }}: {{ band.value }}\n{{ end }}{{ end }}";

            var model = new
            {
                Dimensions = new[]
                {
                    new
                    {
                        DimensionKey = "meaning_transfer",
                        DimensionName = "Meaning transfer",
                        PassThreshold = "Band 2 or above",
                        LevelDescriptions = new Dictionary<string, string>
                        {
                            ["1"] = "Best band text.",
                            ["2"] = "Second band text.",
                        },
                    },
                },
            };

            var result = renderer.Render(template, model);

            Assert.Contains("### Meaning transfer (Band 2 or above)", result);
            Assert.Contains("Band 1: Best band text.", result);
            Assert.Contains("Band 2: Second band text.", result);
        }

        [Fact]
        public void Renders_a_list_of_error_taxonomies_with_member_access()
        {
            var renderer = new PromptRenderer();
            var template = "{{ for cat in error_taxonomies }}- {{ cat.category_key }}({{ cat.category_name }}): {{ cat.description }}\n{{ end }}";

            var model = new
            {
                ErrorTaxonomies = new[]
                {
                    new { CategoryKey = "distortion", CategoryName = "Distortion", Description = "Meaning changed." },
                },
            };

            var result = renderer.Render(template, model);

            Assert.Contains("- distortion(Distortion): Meaning changed.", result);
        }
    }
}
