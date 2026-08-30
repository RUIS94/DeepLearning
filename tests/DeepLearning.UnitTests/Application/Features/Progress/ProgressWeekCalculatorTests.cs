using DeepLearning.Application.Features.Progress;

namespace DeepLearning.UnitTests.Application.Features.Progress
{
    public class ProgressWeekCalculatorTests
    {
        [Fact]
        public void Zero_or_negative_week_count_returns_an_empty_list()
        {
            var today = new DateOnly(2026, 8, 31);

            Assert.Empty(ProgressWeekCalculator.TrailingWeeks(today, 0));
            Assert.Empty(ProgressWeekCalculator.TrailingWeeks(today, -1));
        }

        [Fact]
        public void Single_week_is_the_monday_through_sunday_containing_today()
        {
            // 2026-08-31 is a Monday.
            var monday = new DateOnly(2026, 8, 31);
            var weeks = ProgressWeekCalculator.TrailingWeeks(monday, 1);

            Assert.Single(weeks);
            Assert.Equal(new DateOnly(2026, 8, 31), weeks[0].PeriodStart);
            Assert.Equal(new DateOnly(2026, 9, 6), weeks[0].PeriodEnd);
        }

        [Fact]
        public void A_mid_week_date_still_resolves_to_its_own_weeks_monday_and_sunday()
        {
            // 2026-09-03 is a Thursday within the week starting Monday 2026-08-31.
            var thursday = new DateOnly(2026, 9, 3);
            var weeks = ProgressWeekCalculator.TrailingWeeks(thursday, 1);

            Assert.Single(weeks);
            Assert.Equal(new DateOnly(2026, 8, 31), weeks[0].PeriodStart);
            Assert.Equal(new DateOnly(2026, 9, 6), weeks[0].PeriodEnd);
        }

        [Fact]
        public void Multiple_weeks_are_contiguous_non_overlapping_and_oldest_first()
        {
            var monday = new DateOnly(2026, 8, 31);
            var weeks = ProgressWeekCalculator.TrailingWeeks(monday, 3);

            Assert.Equal(3, weeks.Count);
            // Oldest first: the current week is last.
            Assert.Equal(new DateOnly(2026, 8, 17), weeks[0].PeriodStart);
            Assert.Equal(new DateOnly(2026, 8, 23), weeks[0].PeriodEnd);
            Assert.Equal(new DateOnly(2026, 8, 24), weeks[1].PeriodStart);
            Assert.Equal(new DateOnly(2026, 8, 30), weeks[1].PeriodEnd);
            Assert.Equal(new DateOnly(2026, 8, 31), weeks[2].PeriodStart);
            Assert.Equal(new DateOnly(2026, 9, 6), weeks[2].PeriodEnd);

            // Contiguous: each week's end is exactly one day before the next week's start.
            for (var i = 0; i < weeks.Count - 1; i++)
            {
                Assert.Equal(weeks[i].PeriodEnd.AddDays(1), weeks[i + 1].PeriodStart);
            }
        }
    }
}
