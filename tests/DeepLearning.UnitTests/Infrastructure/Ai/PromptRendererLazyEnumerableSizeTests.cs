using DeepLearning.Infrastructure.Ai;

namespace DeepLearning.UnitTests.Infrastructure.Ai
{
    /// <summary>
    /// Every prompt template in this project gates an optional block with
    /// <c>{{ if some_list.size &gt; 0 }}</c>, and every handler builds those lists with LINQ
    /// <c>.Select(...)</c> — which returns a lazy iterator, not a collection.
    ///
    /// That combination silently evaluated to false in production: meaning checkpoints, weak
    /// points and correction patches were never rendered into a single grading prompt, even when
    /// the database had them. Nothing failed — the block just wasn't there, and the model,
    /// having been told to report checkpoint verdicts, invented thirteen of them.
    ///
    /// These tests pin the behaviour that caused it and the shape that fixes it, so a future
    /// handler that hands Scriban a bare IEnumerable is caught here rather than in a live grading.
    /// </summary>
    public class PromptRendererLazyEnumerableSizeTests
    {
        private const string Template = "{{ if items.size > 0 }}HAS:{{ for i in items }}[{{ i.name }}]{{ end }}{{ else }}EMPTY{{ end }}";

        private static readonly string[] Source = ["a", "b"];

        [Fact]
        public void A_materialised_list_reports_its_size_correctly()
        {
            var model = new { Items = Source.Select(x => new { Name = x }).ToList() };

            var rendered = new PromptRenderer().Render(Template, model);

            Assert.Equal("HAS:[a][b]", rendered);
        }

        [Fact]
        public void An_array_reports_its_size_correctly()
        {
            var model = new { Items = Source.Select(x => new { Name = x }).ToArray() };

            var rendered = new PromptRenderer().Render(Template, model);

            Assert.Equal("HAS:[a][b]", rendered);
        }

        [Fact]
        public void A_lazy_Select_iterator_now_gates_the_block_correctly()
        {
            // This is the production bug, and the reason PromptRenderer materialises enumerables
            // before handing the model to Scriban. Without that step this renders "EMPTY":
            // `for` walks a lazy iterator fine, so the data IS reachable, but `.size` reports 0,
            // the surrounding `if` never opens, and the block vanishes with no error anywhere.
            // That is how grading prompts went out with no meaning checkpoints and no weak points.
            var model = new { Items = Source.Select(x => new { Name = x }) };

            var rendered = new PromptRenderer().Render(Template, model);

            Assert.Equal("HAS:[a][b]", rendered);
        }

        [Fact]
        public void An_empty_lazy_iterator_still_reports_as_empty()
        {
            // Materialising must not turn "no data" into "some data" — the gate has to keep
            // working in the direction it was written for.
            var model = new { Items = Array.Empty<string>().Select(x => new { Name = x }) };

            Assert.Equal("EMPTY", new PromptRenderer().Render(Template, model));
        }

        [Fact]
        public void A_string_property_is_not_mistaken_for_a_list_of_characters()
        {
            var rendered = new PromptRenderer().Render("{{ title }}", new { Title = "Why People Get Sunburn" });

            Assert.Equal("Why People Get Sunburn", rendered);
        }

        [Fact]
        public void A_lazy_iterator_still_renders_through_a_bare_for_loop()
        {
            // Why the damage was selective: blocks written as a plain `{{ for }}` (dimensions,
            // error taxonomies, findings) always worked. Only the `.size`-gated ones vanished.
            var model = new { Items = Source.Select(x => new { Name = x }) };

            var rendered = new PromptRenderer().Render("{{ for i in items }}[{{ i.name }}]{{ end }}", model);

            Assert.Equal("[a][b]", rendered);
        }
    }
}
