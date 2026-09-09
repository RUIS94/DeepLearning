using DeepLearning.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace DeepLearning.Api.Filters
{
    /// <summary>
    /// Gates a controller/action behind a <c>feature_flags</c> key: when the flag is off the
    /// endpoint answers <c>404</c> with a <see cref="ProblemDetails"/> body — the feature reads
    /// as "not here" rather than "forbidden". Put it on the controller class so every action is
    /// covered. Design doc §六 / §11.2 Step 10.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public sealed class FeatureGateAttribute : Attribute, IFilterFactory
    {
        private readonly string _key;

        public FeatureGateAttribute(string key) => _key = key;

        public bool IsReusable => false;

        public IFilterMetadata CreateInstance(IServiceProvider serviceProvider)
            => new FeatureGateFilter(_key, serviceProvider.GetRequiredService<IFeatureFlagService>());

        private sealed class FeatureGateFilter : IAsyncActionFilter
        {
            private readonly string _key;
            private readonly IFeatureFlagService _flags;

            public FeatureGateFilter(string key, IFeatureFlagService flags)
            {
                _key = key;
                _flags = flags;
            }

            public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
            {
                if (!await _flags.IsEnabledAsync(_key, context.HttpContext.RequestAborted))
                {
                    context.Result = new ObjectResult(new ProblemDetails
                    {
                        Status = StatusCodes.Status404NotFound,
                        Title = $"The '{_key}' feature is currently disabled.",
                    })
                    {
                        StatusCode = StatusCodes.Status404NotFound,
                    };
                    return;
                }

                await next();
            }
        }
    }
}
