namespace DeepLearning.Domain.Enums
{
    /// <summary>
    /// The single canonical source for NAATI's official Major / Minor error definitions —
    /// verbatim from the certification glossary. Before this class the exact same English text
    /// lived in two unsynced places: the <see cref="ErrorSeverity"/> enum's XML doc, and the
    /// production grading prompt template (hard-coded three times in one row). Anything that
    /// needs to show the grader — or a follow-up / score-challenge reviewer — what "Major" and
    /// "Minor" actually mean must pull from here, so the wording can never drift between the
    /// grading pass and a later dispute over that same grading.
    ///
    /// Injected into prompt templates via the template model as
    /// <c>{{ major_error_definition }}</c> / <c>{{ minor_error_definition }}</c>.
    /// </summary>
    public static class ErrorSeverityDefinitions
    {
        public const string Major =
            "An error which causes inaccuracies in the propositional content and intent of the " +
            "message to be transferred AND affects the purpose and function/s of the communication, " +
            "and/or which impacts on comprehension of the target text or utterance.";

        public const string Minor =
            "An error which only causes inaccuracies in the propositional content of the message to " +
            "be transferred BUT neither affects the intent of the message nor the function/s of the " +
            "communication, and/or which does not impact on the comprehension of the target text or " +
            "utterance.";
    }
}
