using System.Net;
using DeepLearning.Infrastructure.Ai;

namespace DeepLearning.UnitTests.Infrastructure.Ai
{
    public class LlmResiliencePipelineTests
    {
        // Same retry-count/ShouldHandle logic as production, but a millisecond-scale base
        // delay so the exponential backoff genuinely runs without a multi-second test.
        private static readonly TimeSpan TestBaseDelay = TimeSpan.FromMilliseconds(5);

        [Fact]
        public async Task Retries_on_500_and_eventually_succeeds()
        {
            var pipeline = LlmResiliencePipeline.BuildTestPipeline(TestBaseDelay);
            var attempts = 0;

            var response = await pipeline.ExecuteAsync(async _ =>
            {
                attempts++;
                if (attempts < 3)
                {
                    return new HttpResponseMessage(HttpStatusCode.InternalServerError);
                }
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.OK);
            });

            Assert.Equal(3, attempts);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task Gives_up_after_max_retry_attempts_and_returns_the_last_failure()
        {
            var pipeline = LlmResiliencePipeline.BuildTestPipeline(TestBaseDelay);
            var attempts = 0;

            var response = await pipeline.ExecuteAsync(async _ =>
            {
                attempts++;
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.InternalServerError);
            });

            // MaxRetryAttempts = 3 means 1 initial attempt + 3 retries = 4 total.
            Assert.Equal(4, attempts);
            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [Fact]
        public async Task Does_not_retry_a_client_error()
        {
            var pipeline = LlmResiliencePipeline.BuildTestPipeline(TestBaseDelay);
            var attempts = 0;

            var response = await pipeline.ExecuteAsync(async _ =>
            {
                attempts++;
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.BadRequest);
            });

            Assert.Equal(1, attempts);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }
}
