using DeepLearning.Domain.Common;

namespace DeepLearning.Domain.Entities
{
    public class ReferenceTranslation : Entity
    {
        public Guid QuestionId { get; set; }

        /// <summary>
        /// Translation of the source's own title, kept separate from <see cref="ReferenceText"/>
        /// (the body) so the deep-learning page can present them distinctly and downstream
        /// consumers of the body aren't fed a title line. Null when the source has no title.
        /// </summary>
        public string? ReferenceTitle { get; set; }

        public string ReferenceText { get; set; } = string.Empty;
        public string? ComparisonNotes { get; set; }
        public DateTimeOffset CreatedAt { get; set; }

        public Question? Question { get; set; }
    }
}
