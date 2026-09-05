using DeepLearning.Application.Interfaces;
using NSubstitute;

namespace DeepLearning.UnitTests.TestInfrastructure
{
    /// <summary>
    /// One <see cref="ILlmClientResolver"/> stand-in, replacing the twelve hand-written ones this test
    /// project used to carry.
    ///
    /// <para>Every one of them existed only to hand back a given <see cref="ILlmClient"/> — the
    /// interface has a single member and no behavior worth faking. Three of them
    /// (<c>Fixed*LlmClientResolver</c>) were already this exact generic wrapper, written out three
    /// times; the other nine hard-coded one fake client each, so adding an LLM scenario meant adding
    /// a resolver class alongside it. The scenario lives in the client, which is where the interesting
    /// payloads and edge cases are — the resolver was pure ceremony.</para>
    ///
    /// <para>Note this is deliberately NOT a general move toward mocking. The suite's doubles that carry
    /// real content — <c>FakeGradingPayloads</c>'s boundary-value fixtures, the fixed-JSON clients — stay
    /// hand-written, because reading them IS how you know what the test feeds the system. And nothing
    /// here touches persistence: repositories and DbContext are never substituted, every data test runs
    /// against a real Postgres container.</para>
    /// </summary>
    public static class LlmClientResolverSubstitute
    {
        /// <summary>
        /// A resolver that always hands back <paramref name="client"/> — the same instance every call, so
        /// a client that counts its own invocations (retry-sequence fakes) accumulates across the calls
        /// one request makes.
        /// </summary>
        public static ILlmClientResolver Returning(ILlmClient client)
        {
            var resolver = Substitute.For<ILlmClientResolver>();
            resolver.GetActiveClientAsync(Arg.Any<CancellationToken>()).Returns(client);
            return resolver;
        }
    }
}
