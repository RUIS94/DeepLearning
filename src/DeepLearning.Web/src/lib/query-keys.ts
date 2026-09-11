/**
 * Single source for every React Query key shape used across the app (代码复用扫描_07_优化计划.md
 * §4.1a) — previously ~60 call sites across 24 files each spelled out their own array literal, so
 * a typo in one spot silently created a second, never-invalidated cache entry instead of erroring.
 * Every function here reproduces the EXACT shape the call site already used — this pass is purely
 * mechanical (centralize, don't change what's cached under what key). Merging genuinely-duplicate
 * keys that different pages spell differently (e.g. "categories" vs "admin"+"categories"+id) is a
 * separate, deliberate follow-up (§4.1b) — not done here.
 */
export const qk = {
  currentUser: () => ["current-user"] as const,

  // §4.1b: unified from what used to be two separate namespaces — plain useQuery(["categories"])
  // (no filter) on the student-facing pages vs useQuery(["admin","categories",examTypeId]) on the
  // admin panel — both calling the exact same listCategories(examTypeId?) endpoint. Merging them
  // means editing a category from admin now actually invalidates what practice-page/import-panel
  // have cached, instead of relying on categories-panel.tsx manually invalidating both namespaces.
  categories: (examTypeId?: string | null) => ["categories", examTypeId ?? null] as const,
  /** Prefix-only — matches every categories(...) entry regardless of examTypeId. */
  categoriesAll: () => ["categories"] as const,
  adminQuestionsForTagging: () => ["admin", "questions-for-tagging"] as const,

  weakPoints: (userId: string | undefined, status: number | "all") =>
    ["weak-points", userId, status] as const,
  /** Prefix-only — matches every weakPoints(...) entry regardless of userId/status. */
  weakPointsAll: () => ["weak-points"] as const,
  adminWeakPointCatalog: () => ["admin", "weak-point-catalog"] as const,
  adminWeakPointCategories: () => ["admin", "weak-point-categories"] as const,

  questionsSeeds: () => ["questions", "seeds"] as const,
  questions: (
    taskType: string,
    difficulty: string,
    categoryId: string,
    userId: string | undefined,
  ) => ["questions", taskType, difficulty, categoryId, userId] as const,
  /** Prefix-only — matches both questionsSeeds() and questions(...) at once. */
  questionsAll: () => ["questions"] as const,

  examConfigExamType: () => ["exam-config", "exam-type"] as const,
  examConfigErrorTaxonomies: (examTypeId: string | undefined) =>
    ["exam-config", "error-taxonomies", examTypeId] as const,

  submissionsForQuestion: (userId: string | undefined, questionId: string | null) =>
    ["submissions", userId, questionId] as const,

  assessmentDimensions: (examTypeId: string | undefined) =>
    ["assessment-dimensions", examTypeId] as const,

  followUpThreads: (submissionId: string) => ["follow-up-threads", submissionId] as const,
  followUpThread: (threadId: string | null) => ["follow-up-thread", threadId] as const,

  reviewPatterns: (userId: string | undefined) => ["review-patterns", userId] as const,
  reviewPatternsAll: () => ["review-patterns"] as const,
  reviewVocab: (userId: string | undefined) => ["review-vocab", userId] as const,
  reviewVocabAll: () => ["review-vocab"] as const,

  standardOverrides: (examTypeId: string | null) => ["standard-overrides", examTypeId] as const,
  standardOverridesAll: () => ["standard-overrides"] as const,
  standardOverride: (overrideId: string) => ["standard-override", overrideId] as const,

  adminExamTypes: () => ["admin", "exam-types"] as const,
  adminExamType: (examTypeId: string) => ["admin", "exam-type", examTypeId] as const,
  adminPromptTemplates: (examTypeId: string | null) =>
    ["admin", "prompt-templates", examTypeId] as const,
  /** Prefix-only — matches every adminPromptTemplates(...) entry regardless of examTypeId. */
  adminPromptTemplatesAll: () => ["admin", "prompt-templates"] as const,

  featureFlags: () => ["feature-flags"] as const,

  progress: (userId: string | undefined, difficultyTier: string) =>
    ["progress", userId, difficultyTier] as const,

  submission: (submissionId: string) => ["submission", submissionId] as const,
  question: (questionId: string | undefined) => ["question", questionId] as const,
  gradingStatus: (submissionId: string) => ["grading-status", submissionId] as const,
  seedReferences: (questionId: string) => ["seed-references", questionId] as const,

  adminLlmProviderModels: (providerKey: string | null) =>
    ["admin", "llm-provider-models", providerKey] as const,
  adminLlmProviderSettings: () => ["admin", "llm-provider-settings"] as const,
  adminAiOperationOverrides: () => ["admin", "ai-operation-overrides"] as const,

  deepLearning: (questionId: string) => ["deep-learning", questionId] as const,
};
