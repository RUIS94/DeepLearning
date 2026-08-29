using DeepLearning.Domain.Enums;

namespace DeepLearning.Application.Interfaces
{
    /// <summary>
    /// Turns the AI's raw per-dimension grading output into a stored Band (1-5, the schema's
    /// fixed representation regardless of scale_type — see grading_results.band's CHECK
    /// constraint) plus a PassBool, per assessment_dimensions.scale_type. GradeSubmissionCommandHandler
    /// picks the implementation matching each dimension's ScaleType (via DI's IEnumerable&lt;IGradingResultInterpreter&gt;)
    /// rather than trusting the AI to self-report pass/fail — same "structured output +
    /// code-side validation, not just a prompt reminder" philosophy as error_category (design
    /// doc §10.3).
    /// </summary>
    public interface IGradingResultInterpreter
    {
        ScaleType ScaleType { get; }

        GradingInterpretation Interpret(string rawValue, string? passThreshold);
    }

    public record GradingInterpretation(int Band, bool PassBool);
}
