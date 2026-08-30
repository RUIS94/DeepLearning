namespace DeepLearning.Application.Features.Progress
{
    /// <summary>
    /// Pure ISO-week boundary math for ProgressSnapshotJob's weekly trend generation/backfill —
    /// no DB, no clock injection needed beyond the "today" the caller already has, so it's
    /// directly unit-testable. A "week" here is Monday..Sunday, matching how design doc §6.14's
    /// period_start/period_end are just plain DATE columns with no enforced granularity of their
    /// own — daily rows (Step 6's UpdateProgressOnGraded) and these weekly rollup rows coexist in
    /// the same table, distinguished only by how far apart PeriodStart/PeriodEnd are.
    /// </summary>
    public static class ProgressWeekCalculator
    {
        public record WeekRange(DateOnly PeriodStart, DateOnly PeriodEnd);

        /// <summary>
        /// The trailing <paramref name="weekCount"/> complete ISO weeks up to and including the
        /// week containing <paramref name="today"/>, oldest first — so a job iterating this list
        /// in order always has every earlier week's snapshot already persisted before it needs it
        /// as AI trend context for a later week.
        /// </summary>
        public static List<WeekRange> TrailingWeeks(DateOnly today, int weekCount)
        {
            if (weekCount <= 0)
            {
                return [];
            }

            var currentWeekStart = StartOfWeek(today);
            var weeks = new List<WeekRange>(weekCount);
            for (var i = weekCount - 1; i >= 0; i--)
            {
                var periodStart = currentWeekStart.AddDays(-7 * i);
                var periodEnd = periodStart.AddDays(6);
                weeks.Add(new WeekRange(periodStart, periodEnd));
            }

            return weeks;
        }

        private static DateOnly StartOfWeek(DateOnly date)
        {
            // DayOfWeek.Monday == 1 ... Sunday == 0; ISO weeks start on Monday.
            var offset = ((int)date.DayOfWeek + 6) % 7;
            return date.AddDays(-offset);
        }
    }
}
