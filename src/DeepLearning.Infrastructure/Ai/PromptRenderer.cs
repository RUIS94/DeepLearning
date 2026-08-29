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

            var context = new TemplateContext();
            context.PushGlobal(scriptObject);

            return parsed.Render(context);
        }
    }
}
