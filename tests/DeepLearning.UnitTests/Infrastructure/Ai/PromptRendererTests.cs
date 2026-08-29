using DeepLearning.Infrastructure.Ai;

namespace DeepLearning.UnitTests.Infrastructure.Ai
{
    public class PromptRendererTests
    {
        private readonly PromptRenderer _renderer = new();

        [Fact]
        public void Renders_a_simple_placeholder_from_a_pascal_case_property()
        {
            var result = _renderer.Render("Difficulty: {{ difficulty }}", new { Difficulty = "medium" });

            Assert.Equal("Difficulty: medium", result);
        }

        [Fact]
        public void Renders_a_for_loop_over_a_list_of_objects_like_the_seeded_grading_template()
        {
            const string template = "{{ for dim in dimensions }}{{ dim.dimension_name }}({{ dim.pass_threshold }}) {{ end }}";
            var model = new
            {
                Dimensions = new[]
                {
                    new { DimensionName = "Meaning transfer", PassThreshold = "Band 2 or above" },
                    new { DimensionName = "Textual norms", PassThreshold = "Band 3 or above" },
                },
            };

            var result = _renderer.Render(template, model);

            Assert.Equal("Meaning transfer(Band 2 or above) Textual norms(Band 3 or above) ", result);
        }

        [Fact]
        public void Throws_when_the_template_has_a_syntax_error()
        {
            Assert.Throws<InvalidOperationException>(() => _renderer.Render("{{ for x in }}", new { }));
        }
    }
}
