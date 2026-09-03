namespace DeepLearning.Domain.Enums
{
    /// <summary>
    /// Per-error gravity, judged by the grader on each error_list row. Feeds two things:
    /// the frontend's severity badge, and the "累积密度" (cumulative density) judgment —
    /// a cluster of <see cref="moderate"/>+ errors on one dimension is itself a downgrade
    /// reason even when no single one is fatal (design doc §10.1 / 原则2). The
    /// impact-on-core-message label shown in the UI is derived from this, not stored
    /// separately: major/critical -> 影响核心意义点, moderate -> 接近边界, minor -> 非核心.
    /// </summary>
    public enum ErrorSeverity
    {
        minor,
        moderate,
        major,
        critical
    }
}
