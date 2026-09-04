using System.Collections;
using Scriban;
using Scriban.Runtime;

namespace DeepLearning.Infrastructure.Ai
{
    /// <summary>
    /// Renders a prompt_templates.template_content string (Scriban syntax) against a model
    /// object. Scriban's default member renamer converts PascalCase properties to the
    /// snake_case names the seeded templates use ({{ difficulty }}, {{ dim.dimension_name }}).
    /// </summary>
    public class PromptRenderer
    {
        public string Render(string template, object model)
        {
            var parsed = Template.Parse(template);
            if (parsed.HasErrors)
            {
                throw new InvalidOperationException(
                    $"Prompt template failed to parse: {string.Join("; ", parsed.Messages)}");
            }

            var scriptObject = new ScriptObject();
            scriptObject.Import(model, renamer: StandardMemberRenamer.Rename);
            MaterialiseEnumerables(scriptObject);

            var context = new TemplateContext();
            context.PushGlobal(scriptObject);

            return parsed.Render(context);
        }

        /// <summary>
        /// Replaces every lazily-enumerated property value with a materialised list.
        ///
        /// <para>Scriban's <c>.size</c> reports 0 for a bare <see cref="IEnumerable"/> such as the
        /// iterator LINQ's <c>Select</c> returns — it only counts what it can count without
        /// enumerating. Every handler here builds its template model with <c>.Select(...)</c>, and
        /// every template gates its optional blocks with <c>{{ if some_list.size &gt; 0 }}</c>, so
        /// those blocks silently rendered as if the data were empty while a plain <c>{{ for }}</c>
        /// over the same property worked fine. Nothing threw; the section was simply absent.</para>
        ///
        /// <para>The damage was real and invisible: grading prompts went out with no meaning
        /// checkpoints, no weak points and no correction patches, and the model — still asked for
        /// checkpoint verdicts by the output contract — invented them. 13 such gates exist across
        /// the six active template types.</para>
        ///
        /// <para>Fixed here rather than by adding <c>.ToList()</c> in each handler (AGENTS.md #4:
        /// isolation should be structural) — a convention that every future template model must
        /// remember to materialise would eventually be bypassed, and the failure mode is silent.
        /// Only top-level properties are walked, which covers every <c>.size</c> gate in use;
        /// strings and anything already countable are left untouched.</para>
        /// </summary>
        private static void MaterialiseEnumerables(ScriptObject scriptObject)
        {
            foreach (var key in scriptObject.Keys.ToList())
            {
                if (scriptObject[key] is IEnumerable value and not string and not ICollection)
                {
                    scriptObject[key] = value.Cast<object?>().ToList();
                }
            }
        }
    }
}
