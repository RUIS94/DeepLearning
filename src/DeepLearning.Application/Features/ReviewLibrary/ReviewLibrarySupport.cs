namespace DeepLearning.Application.Features.ReviewLibrary
{
    /// <summary>
    /// Shared plumbing for the review-library handlers — two shapes that were each hand-copied
    /// across 4-6 places (代码复用扫描_07_优化计划.md §3.5/R-A-13/N5):
    ///
    /// <para><see cref="UpsertReviewAsync{TReview}"/> — "get the existing user-review row for this
    /// (user, item) pair, or create+persist a new one; otherwise adjust the existing one in place."
    /// Used by ExtractKnowledgePointsOnGraded (auto-tracking: increments TimesEncountered, leaves
    /// MasteryLevel alone) and MarkVocabReviewedCommandHandler/MarkPatternReviewedCommandHandler
    /// (explicit user action: sets MasteryLevel, does NOT increment TimesEncountered) — the two
    /// families differ in what "existing" means, so that stays a caller-supplied delegate rather
    /// than baked into this helper.</para>
    ///
    /// <para><see cref="ProjectWithReview{TItem,TReview,TKey,TResult}"/> — "list catalog items,
    /// list this user's reviews for them, join by key, default a missing review to
    /// (0 encounters, New, null LastReviewedAt)." Used by ListReviewPatternsQueryHandler and
    /// ListReviewVocabQueryHandler.</para>
    /// </summary>
    internal static class ReviewLibrarySupport
    {
        public static async Task<TReview> UpsertReviewAsync<TReview>(
            TReview? existing,
            Func<TReview> createNew,
            Action<TReview> updateExisting,
            Func<TReview, CancellationToken, Task> addAsync,
            CancellationToken cancellationToken)
            where TReview : class
        {
            if (existing is null)
            {
                var created = createNew();
                await addAsync(created, cancellationToken);
                return created;
            }

            updateExisting(existing);
            return existing;
        }

        public static List<TResult> ProjectWithReview<TItem, TReview, TKey, TResult>(
            IEnumerable<TItem> items,
            IEnumerable<TReview> reviews,
            Func<TItem, TKey> itemKey,
            Func<TReview, TKey> reviewKey,
            Func<TItem, TReview?, TResult> project)
            where TKey : notnull
        {
            var reviewsByKey = reviews.ToDictionary(reviewKey);
            return items.Select(item =>
            {
                reviewsByKey.TryGetValue(itemKey(item), out var review);
                return project(item, review);
            }).ToList();
        }
    }
}
