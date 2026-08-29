using Serilog.Context;

namespace DeepLearning.Api.Middleware
{
    /// <summary>
    /// 给每个请求分配/透传一个correlation id,写入响应头并推入Serilog的LogContext,
    /// 为将来"请求全链路trace_id贯穿前端→后端→AI调用"的可观测性需求打底。
    /// </summary>
    public class CorrelationIdMiddleware
    {
        public const string HeaderName = "X-Correlation-Id";
        public const string HttpContextItemKey = "CorrelationId";

        private readonly RequestDelegate _next;

        public CorrelationIdMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var correlationId = context.Request.Headers.TryGetValue(HeaderName, out var existing) && !string.IsNullOrWhiteSpace(existing)
                ? existing.ToString()
                : Guid.NewGuid().ToString();

            context.Items[HttpContextItemKey] = correlationId;
            context.Response.Headers[HeaderName] = correlationId;

            using (LogContext.PushProperty("CorrelationId", correlationId))
            {
                await _next(context);
            }
        }
    }
}
