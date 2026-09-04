namespace DeepLearning.Domain.Enums
{
    /// <summary>
    /// NAATI's own two error levels, quoted from the certification glossary:
    ///
    /// <para><b>Major</b> — "An error which causes inaccuracies in the propositional content and
    /// intent of the message to be transferred AND affects the purpose and function/s of the
    /// communication, and/or which impacts on comprehension of the target text or utterance."</para>
    ///
    /// <para><b>Minor</b> — "An error which only causes inaccuracies in the propositional content
    /// of the message to be transferred BUT neither affects the intent of the message nor the
    /// function/s of the communication, and/or which does not impact on the comprehension of the
    /// target text or utterance."</para>
    ///
    /// <para>There used to be four values: minor / moderate / major / critical, invented here as
    /// subdivisions of the official two. They cost more than they were worth. critical needed
    /// three conditions at once and turned out to be unreachable; moderate swallowed everything,
    /// so across 55 real findings the distribution was 40 moderate, 14 major, 1 critical and
    /// <b>zero</b> minor — a four-point scale being used as a two-point one, with both points in
    /// the wrong place. Grading them against the official definition is both simpler and the only
    /// thing the rubric actually asks for: an error either impacts intent/function/comprehension
    /// or it does not.</para>
    ///
    /// <para>Feeds the frontend badge and the "impacts the core message" reading the verdict
    /// stage checks — an error is core iff it is <see cref="major"/>.</para>
    /// </summary>
    public enum ErrorSeverity
    {
        /// <summary>Propositional inaccuracy only. Intent, function and comprehension all intact.</summary>
        minor,

        /// <summary>Changes the intent or purpose/function, and/or impacts comprehension.</summary>
        major,
    }
}
