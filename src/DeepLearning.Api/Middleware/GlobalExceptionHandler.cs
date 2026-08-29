using DeepLearning.Domain.Exceptions;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace DeepLearning.Api.Middleware
{
    /// <summary>
    /// 全局异常到HTTP响应的映射:领域/校验异常翻译成对应状态码的ProblemDetails,
    /// 其余未预期异常一律500,不把内部异常细节泄露给客户端。
    /// </summary>
    public class GlobalExceptionHandler : IExceptionHandler
    {
        private readonly ILogger<GlobalExceptionHandler> _logger;

        public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
        {
            _logger = logger;
        }

        public async ValueTask<bool> TryHandleAsync(
            HttpContext httpContext,
            Exception exception,
            CancellationToken cancellationToken)
        {
            var (statusCode, title, errors) = exception switch
            {
                ValidationException validationException => (
                    StatusCodes.Status400BadRequest,
                    "Validation failed",
                    (IDictionary<string, string[]>?)validationException.Errors
                        .GroupBy(e => e.PropertyName)
                        .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray())),
                NotFoundException notFoundException => (StatusCodes.Status404NotFound, notFoundException.Message, null),
                ConflictException conflictException => (StatusCodes.Status409Conflict, conflictException.Message, null),
                AiCallFailedException aiCallFailedException => (StatusCodes.Status503ServiceUnavailable, aiCallFailedException.Message, null),
                DomainException domainException => (StatusCodes.Status400BadRequest, domainException.Message, null),
                _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred.", null),
            };

            if (statusCode == StatusCodes.Status500InternalServerError)
            {
                _logger.LogError(exception, "Unhandled exception processing {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);
            }

            var problemDetails = new ProblemDetails
            {
                Status = statusCode,
                Title = title,
                Extensions = { ["correlationId"] = httpContext.Items[CorrelationIdMiddleware.HttpContextItemKey] },
            };

            if (errors is not null)
            {
                problemDetails.Extensions["errors"] = errors;
            }

            httpContext.Response.StatusCode = statusCode;
            await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

            return true;
        }
    }
}
