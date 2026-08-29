namespace DeepLearning.Domain.Exceptions
{
    /// <summary>
    /// Thrown when an AI grading response references a dimension_key that has no matching,
    /// currently-effective AssessmentDimension row for the exam type/task type being graded —
    /// i.e. there is no rubric version on file to hold the AI to for that dimension. This is a
    /// structural-output validation failure (design doc §10.3's "hard constraint, not a prompt
    /// reminder" philosophy, applied to dimensions the same way error_category is validated
    /// against error_taxonomies), not a normal 404 on a resource the caller asked for by id —
    /// but it reuses NotFoundException's 404 mapping since "no such rubric on file" is the same
    /// shape of problem.
    /// </summary>
    public class RubricVersionNotFoundException : NotFoundException
    {
        public RubricVersionNotFoundException(string dimensionKey)
            : base(nameof(Entities.AssessmentDimension), dimensionKey)
        {
        }
    }
}
