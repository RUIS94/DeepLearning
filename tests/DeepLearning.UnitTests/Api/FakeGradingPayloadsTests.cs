using System.Text.Json;

namespace DeepLearning.UnitTests.Api
{
    /// <summary>
    /// The grading fakes drive every integration test that grades a submission, and they are
    /// hand-written JSON inside C# raw string literals — a combination that fails quietly. One
    /// version of this file used a <c>""""..."""" </c> constant whose content began with a quote,
    /// so the compiler read a five-quote delimiter and the emitted JSON was subtly wrong; every
    /// grading test then failed with "evidence stage returned no per-sentence coverage rows"
    /// three layers away from the cause.
    ///
    /// These tests check the fixtures themselves, so a broken fixture says so directly instead of
    /// looking like a broken pipeline. They also pin the dual-shape trick the fixture relies on:
    /// one payload that satisfies both the collection contract and the verdict contract, so no
    /// fake has to guess which of the four stages it is answering.
    /// </summary>
    public class FakeGradingPayloadsTests
    {
        private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

        [Fact]
        public void One_payload_satisfies_the_collection_contract()
        {
            using var doc = JsonDocument.Parse(FakeGradingPayloads.Build("meaning_transfer", "distortion"));

            var sentences = doc.RootElement.GetProperty("sentences");
            Assert.Equal(1, sentences.GetArrayLength());
            Assert.Equal(1, sentences[0].GetProperty("n").GetInt32());

            var finding = Assert.Single(doc.RootElement.GetProperty("findings").EnumerateArray().ToList());
            Assert.Equal("distortion", finding.GetProperty("errorCategory").GetString());
            Assert.Equal("meaning_transfer", finding.GetProperty("dimensionKey").GetString());

            // Both false is NAATI's Minor once derived. Both must be PRESENT: an absent answer
            // is rejected by NormaliseQuestionScheme rather than defaulted, so a fake that
            // stopped emitting one would fail every grading test with the same message.
            Assert.False(finding.GetProperty("q1").GetBoolean());
            Assert.False(finding.GetProperty("q2").GetBoolean());
        }

        [Fact]
        public void The_same_payload_satisfies_the_verdict_contract()
        {
            using var doc = JsonDocument.Parse(FakeGradingPayloads.Build("meaning_transfer", "distortion", band: 2));

            var dimension = Assert.Single(doc.RootElement.GetProperty("dimensions").EnumerateArray().ToList());
            Assert.Equal("meaning_transfer", dimension.GetProperty("dimensionKey").GetString());
            Assert.Equal(2, dimension.GetProperty("band").GetInt32());
            Assert.Equal("high", dimension.GetProperty("confidence").GetString());
        }

        [Fact]
        public void An_out_of_range_band_reaches_the_verdict_block_intact()
        {
            // The fixture behind "the handler rejects a band outside 1-5 before it reaches the DB".
            using var doc = JsonDocument.Parse(FakeGradingPayloads.Build("meaning_transfer", "distortion", band: 9));

            Assert.Equal(9, doc.RootElement.GetProperty("dimensions")[0].GetProperty("band").GetInt32());
        }

        [Fact]
        public void A_null_category_produces_a_clean_run_with_no_findings()
        {
            // For tests that seed a dimension but no error taxonomy and only need the submission
            // to reach Graded — a finding citing an unseeded category would be rejected by a hard
            // constraint those tests are not about.
            using var doc = JsonDocument.Parse(FakeGradingPayloads.Build("meaning_transfer", errorCategoryKey: null));

            Assert.Empty(doc.RootElement.GetProperty("findings").EnumerateArray());
            Assert.Equal("ok", doc.RootElement.GetProperty("sentences")[0].GetProperty("status").GetString());
            Assert.Single(doc.RootElement.GetProperty("dimensions").EnumerateArray().ToList());
        }

        [Fact]
        public void The_payload_parses_with_the_same_options_the_handler_uses()
        {
            var json = FakeGradingPayloads.Build("meaning_transfer", "distortion");
            var parsed = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, Options);

            Assert.NotNull(parsed);
            Assert.True(parsed!.ContainsKey("findings"));
            Assert.True(parsed.ContainsKey("dimensions"));
        }
    }
}
