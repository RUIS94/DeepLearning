using DeepLearning.Domain.Exceptions;

namespace DeepLearning.Application.Common
{
    /// <summary>
    /// Collapses the "look up a row, 404 if it isn't there, discard the row itself" idiom used at
    /// call sites that only need to verify a foreign key target exists — not read anything off it
    /// (代码复用扫描_07_优化计划.md §2.3/N10). This is deliberately narrow: a lookup whose result IS
    /// used afterward keeps its explicit `?? throw new NotFoundException(...)` — that form stays
    /// the idiomatic, self-documenting one this codebase prefers whenever the loaded value matters,
    /// and it was explicitly decided NOT to collapse that general case behind a `Guard.NotFound`
    /// indirection (see the plan doc's §2.3 write-up).
    /// </summary>
    public static class NotFoundGuard
    {
        public static async Task EnsureFoundAsync<T>(this Task<T?> lookup, string entityName, object key)
            where T : class
        {
            if (await lookup is null)
            {
                throw new NotFoundException(entityName, key);
            }
        }
    }
}
