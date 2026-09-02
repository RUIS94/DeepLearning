using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace DeepLearning.Infrastructure.Ai
{
    /// <summary>
    /// Turns the raw request/response JSON that <see cref="AiTracingHandler"/> captures into a
    /// plain-text, CJK-literal transcript a human can skim — no <c>\uXXXX</c> escapes, real line
    /// breaks, just the system/user prompts going in and the assistant text (plus any thinking)
    /// coming back.
    ///
    /// Understands both request shapes this codebase sends — Claude's <c>POST /v1/messages</c>
    /// (<c>system</c> + <c>messages[]</c>, <c>content[]</c> response blocks) and the shared
    /// OpenAI-compatible <c>POST /chat/completions</c> (<c>messages[]</c>, <c>choices[].message</c>
    /// response) — and falls back to pretty-printed JSON for anything it does not recognise, so it
    /// can never silently hide content by failing to parse.
    /// </summary>
    public static class AiTraceRenderer
    {
        private static readonly JsonSerializerOptions PrettyOptions = new()
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        private static readonly JsonSerializerOptions CompactOptions = new()
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        /// <summary>Re-indents JSON with real Unicode characters; returns the input untouched if it does not parse.</summary>
        public static string PrettyJson(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return "(empty)";
            }

            try
            {
                using var doc = JsonDocument.Parse(json);
                return JsonSerializer.Serialize(doc.RootElement, PrettyOptions);
            }
            catch (JsonException)
            {
                return json;
            }
        }

        /// <summary>Renders an outbound LLM request body as "model/params header + ### SYSTEM / ### USER sections".</summary>
        public static string RenderRequest(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return "(no request body)";
            }

            JsonElement root;
            try
            {
                using var doc = JsonDocument.Parse(json);
                root = doc.RootElement.Clone();
            }
            catch (JsonException)
            {
                return json;
            }

            if (root.ValueKind != JsonValueKind.Object)
            {
                return PrettyJson(json);
            }

            var sb = new StringBuilder();

            AppendScalar(sb, root, "model");
            AppendScalar(sb, root, "max_tokens");
            AppendScalar(sb, root, "max_completion_tokens");
            AppendScalar(sb, root, "temperature");
            AppendScalar(sb, root, "thinking_enabled");
            if (root.TryGetProperty("thinking", out var thinking))
            {
                sb.Append("thinking: ").Append(Compact(thinking)).Append('\n');
            }
            if (root.TryGetProperty("output_config", out var outputConfig))
            {
                sb.Append("output_config: ").Append(Compact(outputConfig)).Append('\n');
            }

            if (sb.Length > 0)
            {
                sb.Append('\n');
            }

            if (root.TryGetProperty("system", out var system))
            {
                sb.Append("### SYSTEM\n").Append(FlattenContent(system)).Append("\n\n");
            }

            if (root.TryGetProperty("messages", out var messages) && messages.ValueKind == JsonValueKind.Array)
            {
                foreach (var message in messages.EnumerateArray())
                {
                    var role = message.TryGetProperty("role", out var r) ? r.GetString() : null;
                    sb.Append("### ").Append((role ?? "?").ToUpperInvariant()).Append('\n');
                    if (message.TryGetProperty("content", out var content))
                    {
                        sb.Append(FlattenContent(content));
                    }
                    sb.Append("\n\n");
                }
            }

            return sb.Length == 0 ? PrettyJson(json) : sb.ToString().TrimEnd() + "\n";
        }

        /// <summary>Renders an LLM response body down to the assistant text, any thinking/reasoning, and a usage line.</summary>
        public static string RenderResponse(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return "(no response body)";
            }

            JsonElement root;
            try
            {
                using var doc = JsonDocument.Parse(json);
                root = doc.RootElement.Clone();
            }
            catch (JsonException)
            {
                return json;
            }

            if (root.ValueKind != JsonValueKind.Object)
            {
                return PrettyJson(json);
            }

            var sb = new StringBuilder();

            // Both providers report failures as {"error": {...}}.
            if (root.TryGetProperty("error", out var error))
            {
                return sb.Append("### ERROR\n").Append(PrettyJson(error.GetRawText())).Append('\n').ToString();
            }

            if (root.TryGetProperty("choices", out var choices) && choices.ValueKind == JsonValueKind.Array)
            {
                // OpenAI-compatible: choices[].message.{reasoning_content|reasoning, content}
                foreach (var choice in choices.EnumerateArray())
                {
                    if (choice.TryGetProperty("message", out var message))
                    {
                        AppendReasoning(sb, message, "reasoning_content");
                        AppendReasoning(sb, message, "reasoning");
                        if (message.TryGetProperty("content", out var content))
                        {
                            sb.Append("### ASSISTANT\n").Append(FlattenContent(content)).Append("\n\n");
                        }
                    }

                    if (choice.TryGetProperty("finish_reason", out var finishReason) && finishReason.ValueKind == JsonValueKind.String)
                    {
                        sb.Append("(finish_reason: ").Append(finishReason.GetString()).Append(")\n\n");
                    }
                }
            }
            else if (root.TryGetProperty("content", out var blocks) && blocks.ValueKind == JsonValueKind.Array)
            {
                // Claude: content[] blocks of type thinking / text.
                foreach (var block in blocks.EnumerateArray())
                {
                    var type = block.TryGetProperty("type", out var t) ? t.GetString() : null;
                    if (type == "thinking" && block.TryGetProperty("thinking", out var th) && th.ValueKind == JsonValueKind.String)
                    {
                        sb.Append("### ASSISTANT (thinking)\n").Append(th.GetString()).Append("\n\n");
                    }
                    else if (block.TryGetProperty("text", out var tx) && tx.ValueKind == JsonValueKind.String)
                    {
                        sb.Append("### ASSISTANT\n").Append(tx.GetString()).Append("\n\n");
                    }
                }

                if (root.TryGetProperty("stop_reason", out var stopReason) && stopReason.ValueKind == JsonValueKind.String)
                {
                    sb.Append("(stop_reason: ").Append(stopReason.GetString()).Append(")\n\n");
                }
            }
            else
            {
                return PrettyJson(json);
            }

            if (root.TryGetProperty("usage", out var usage))
            {
                sb.Append("usage: ").Append(Compact(usage)).Append('\n');
            }

            return sb.Length == 0 ? PrettyJson(json) : sb.ToString().TrimEnd() + "\n";
        }

        private static void AppendReasoning(StringBuilder sb, JsonElement message, string propertyName)
        {
            if (message.TryGetProperty(propertyName, out var value)
                && value.ValueKind == JsonValueKind.String
                && !string.IsNullOrEmpty(value.GetString()))
            {
                sb.Append("### ASSISTANT (thinking)\n").Append(value.GetString()).Append("\n\n");
            }
        }

        private static string FlattenContent(JsonElement content)
        {
            switch (content.ValueKind)
            {
                case JsonValueKind.String:
                    return content.GetString() ?? string.Empty;

                case JsonValueKind.Array:
                    var parts = new List<string>();
                    foreach (var item in content.EnumerateArray())
                    {
                        if (item.ValueKind == JsonValueKind.String)
                        {
                            parts.Add(item.GetString() ?? string.Empty);
                        }
                        else if (item.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String)
                        {
                            parts.Add(text.GetString() ?? string.Empty);
                        }
                        else if (item.TryGetProperty("thinking", out var thinking) && thinking.ValueKind == JsonValueKind.String)
                        {
                            parts.Add(thinking.GetString() ?? string.Empty);
                        }
                        else
                        {
                            parts.Add(Compact(item));
                        }
                    }
                    return string.Join("\n", parts);

                default:
                    return Compact(content);
            }
        }

        private static void AppendScalar(StringBuilder sb, JsonElement obj, string name)
        {
            if (obj.TryGetProperty(name, out var value)
                && value.ValueKind is not JsonValueKind.Object and not JsonValueKind.Array)
            {
                sb.Append(name).Append(": ").Append(Scalar(value)).Append('\n');
            }
        }

        private static string Scalar(JsonElement value)
            => value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : value.GetRawText();

        private static string Compact(JsonElement element)
            => JsonSerializer.Serialize(element, CompactOptions);
    }
}
