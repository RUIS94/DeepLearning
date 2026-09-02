// 与后端 record 一一对应的 DTO（camelCase JSON）

export interface ProblemDetails {
  status: number;
  title: string;
  correlationId?: string;
  errors?: Record<string, string[]>;
}

/** 对应后端 ListExamTypesResultItem（GET /exam-types 列表项）——发现于真实联调：以前这里被错误地
 * 当成了 GetExamTypeByIdResult 的完整形状，实际 List 接口根本不返回 sourceLanguage/targetLanguage/
 * gradeLevel/description/createdAt，这几个字段只有 GetById 才有（见下面的 ExamTypeDetail）。 */
export interface ExamType {
  id: string;
  code: string;
  name: string;
  subjectCategory: number;
  description: string | null;
  isActive: boolean;
}

/** 对应后端 GetExamTypeByIdResult（GET /exam-types/{id}）。 */
export interface ExamTypeDetail extends ExamType {
  sourceLanguage: string | null;
  targetLanguage: string | null;
  gradeLevel: string | null;
  description: string | null;
  createdAt: string;
}

/** 对应后端 CreateExamTypeResult——比 ExamTypeDetail 少 sourceLanguage/targetLanguage/gradeLevel/
 * description，比 ExamType 列表项多一个 createdAt。 */
export interface CreateExamTypeResult extends ExamType {
  createdAt: string;
}

export interface AssessmentDimension {
  id: string;
  examTypeId: string;
  dimensionKey: string;
  dimensionName: string;
  scaleType: number;
  passThreshold: string | null;
  applicableTaskType: number | null;
  levelDescriptions: string;
  rubricVersion: string;
  effectiveFrom: string;
  effectiveTo: string | null;
  sourceReference: string | null;
  verifiedAt: string | null;
}

export interface ErrorTaxonomy {
  id: string;
  examTypeId: string;
  categoryKey: string;
  categoryName: string;
  description: string | null;
  exampleCases: string | null;
}

export interface QuestionBankCategory {
  id: string;
  categoryType: number;
  name: string;
  parentId: string | null;
  description: string | null;
}

export interface MeaningCheckpoint {
  id: string;
  checkpointText: string;
  checkpointType: string | null;
  importance: number;
}

export interface SeededError {
  id: string;
  positionStart: number;
  positionEnd: number;
  errorTaxonomyId: string;
  errorCategoryKey: string;
  correctReferenceText: string;
  note: string | null;
}

export interface QuestionDetail {
  id: string;
  taskType: number;
  difficulty: number;
  title: string;
  brief: string | null;
  sourceText: string;
  wordCount: number | null;
  origin: number;
  sourceType: number;
  isSeedReference: boolean;
  inBank: boolean;
  visibility: number;
  createdBy: string | null;
  createdAt: string;
  isActive: boolean;
  meaningCheckpoints: MeaningCheckpoint[];
  taskB: { flawedTranslationText: string; seededErrors: SeededError[] } | null;
  categoryIds: string[];
}

export interface QuestionListItem {
  id: string;
  taskType: number;
  difficulty: number;
  title: string;
  wordCount: number | null;
  inBank: boolean;
  createdAt: string;
  /** 当前用户对这道题的提交次数(未带 userId 查询时为 0)。 */
  myAttemptCount: number;
  /** 当前用户对这道题最近一次提交的 id(没练过为 null)。 */
  myLatestSubmissionId: string | null;
}

/** GET /submissions?userId=&questionId= 的列表项(不含评分明细,点开才拉 SubmissionDetail)。 */
export interface SubmissionSummary {
  id: string;
  questionId: string;
  taskType: number;
  status: number;
  submittedAt: string | null;
  createdAt: string;
}

/** 对应后端 SeedReferenceLinkResultItem（GET /questions/{id}/seed-references）——记录 AI 出题时参考了哪些真题。 */
export interface SeedReferenceLink {
  id: string;
  seedQuestionId: string;
  seedQuestionTitle: string;
  similarityReason: string | null;
  createdAt: string;
}

export interface GenerateQuestionRequest {
  examTypeId: string;
  taskType: number;
  difficulty?: number | null;
  categoryId?: string | null;
  seedQuestionIds?: string[] | null;
  createdBy?: string | null;
  targetWeakPoints?: boolean;
}

/** 对应后端 GenerateQuestionResult / ImportUserQuestionResult——两者形状相同，都很薄
 * （不含 sourceText/meaningCheckpoints/taskB 等），完整详情要另外 GET /questions/{id}。 */
export interface CreateQuestionResult {
  id: string;
  taskType: number;
  difficulty: number;
  title: string;
  createdAt: string;
}

export interface TaskBAnnotation {
  positionStart: number;
  positionEnd: number;
  errorCategory: string;
  correctedText: string;
}

export interface CreateSubmissionRequest {
  questionId: string;
  userId: string;
  taskType: number;
  /** JSON 编码后的字符串：TaskA 为 JSON 字符串，TaskB 为 TaskBAnnotation[] */
  content: string;
}

/** 对应后端 CreateSubmissionResult ——刻意很薄，不含 content/gradingResults/errorList。 */
export interface CreateSubmissionResult {
  id: string;
  questionId: string;
  taskType: number;
  status: number;
  submittedAt: string | null;
}

/** 对应后端 GradeSubmissionResult ——同样很薄，只有计数，评分结果需要再 GET 一次 /submissions/{id}。 */
export interface GradeSubmissionResult {
  submissionId: string;
  status: number;
  gradingResultCount: number;
  errorListCount: number;
}

export interface GradingResultItem {
  id: string;
  dimensionKey: string;
  dimensionName: string;
  rubricVersion: string;
  band: number;
  passBool: boolean;
  rationale: string;
  cumulativeDensityFlag: boolean;
  cumulativeDensityNote: string | null;
  estimatedPassProbability: number | null;
}

export interface ErrorListItem {
  id: string;
  positionRef: string | null;
  sourceTextSnippet: string | null;
  userTextSnippet: string | null;
  errorCategory: string;
  dimensionKey: string;
  impactsCore: boolean;
  explanation: string | null;
  suggestion: string | null;
}

/** 对应后端 GradingSummaryResult ——整篇译文的总体判断，位于各维度评分之上。 */
export interface GradingSummary {
  overallPassProbability: number;
  overallPassBool: boolean;
  cumulativeDensityFlag: boolean;
  cumulativeDensityNote: string | null;
  conclusionText: string | null;
}

export interface SubmissionDetail {
  id: string;
  questionId: string;
  userId: string;
  taskType: number;
  content: string;
  status: number;
  submittedAt: string | null;
  createdAt: string;
  gradingResults: GradingResultItem[];
  errorList: ErrorListItem[];
  overallSummary: GradingSummary | null;
}

export interface CreateFollowUpQuestionRequest {
  submissionId: string;
  userId: string;
  examTypeId: string;
  contextRef: string | null;
  questionText: string;
}

export interface FollowUpQuestionResult {
  id: string;
  submissionId: string;
  verdict: number;
  aiResponse: string;
  submissionStatus: number;
  standardOverrideId: string | null;
  standardOverrideStatus: number | null;
}

/** 对应后端 FollowUpQuestionResultItem（GET /follow-ups?submissionId=）与 GetFollowUpQuestionByIdResult（GET /follow-ups/{id}）——两者字段完全一致，共用一个类型。 */
export interface FollowUpQuestionDetail {
  id: string;
  submissionId: string;
  userId: string;
  contextRef: string | null;
  questionText: string;
  aiResponse: string | null;
  verdict: number;
  createdAt: string;
}

export interface CreateFollowUpThreadRequest {
  submissionId: string;
  userId: string;
  examTypeId: string;
  contextRef: string | null;
  questionText: string;
}

export interface AddFollowUpMessageRequest {
  userId: string;
  questionText: string;
}

/** 对应后端 FollowUpMessageResult。role/verdict 是数字序数——见 enums.ts 的 FollowUpMessageRole/FollowUpVerdict。verdict 仅 AI 消息非空，且只是这一轮的看法，不驱动任何副作用（见后端 FollowUpMessage 的注释）。 */
export interface FollowUpMessageDetail {
  id: string;
  role: number;
  content: string;
  verdict: number | null;
  createdAt: string;
}

/** 对应后端 FollowUpThreadSummary（GET /follow-up-threads?submissionId= 列表项）。firstQuestion 是开头那条用户消息，用于列表里做可读标签。 */
export interface FollowUpThreadSummary {
  id: string;
  status: number;
  finalVerdict: number | null;
  standardOverrideId: string | null;
  messageCount: number;
  firstQuestion: string;
  createdAt: string;
  closedAt: string | null;
}

/**
 * 对应后端 FollowUpThreadResult（POST /follow-up-threads、POST /follow-up-threads/{id}/messages、
 * POST /follow-up-threads/{id}/close、GET /follow-up-threads/{id} 共用同一形状）。
 * 一个 submission 可有多条线程，但同时只有一条 open；线程存续期间 submission 停在
 * under_dispute。finalVerdict 为 null 表示这次追问没有质疑任何评判（纯知识咨询）；
 * standardOverrideId 只在 finalVerdict=user_correct 时有值。
 */
export interface FollowUpThreadDetail {
  id: string;
  submissionId: string;
  userId: string;
  contextRef: string | null;
  status: number;
  finalVerdict: number | null;
  standardOverrideId: string | null;
  standardOverrideStatus: number | null;
  submissionStatus: number;
  createdAt: string;
  closedAt: string | null;
  messages: FollowUpMessageDetail[];
}

/** 对应后端 StandardOverrideResultItem（GET /standard-overrides 列表项）。没有 submissionId——
 * 与提交的关联是间接的，经 triggeredByFollowupId → follow-up 的 submissionId。 */
export interface StandardOverride {
  id: string;
  scope: number;
  dimensionOrRule: string;
  revisedRuleText: string;
  status: number;
  previousOverrideId: string | null;
  effectiveFrom: string | null;
  createdAt: string;
}

/** 对应后端 GetStandardOverrideByIdResult，比列表项多 originalRuleText/triggeredByFollowupId。 */
export interface StandardOverrideDetail extends StandardOverride {
  originalRuleText: string | null;
  triggeredByFollowupId: string | null;
}

/** 对应后端 ActivateStandardOverrideResult（POST /standard-overrides/{id}/activate）。 */
export interface ActivateStandardOverrideResult {
  id: string;
  status: number;
  effectiveFrom: string | null;
}

export interface SentencePattern {
  id: string;
  patternName: string;
  exampleSentence: string | null;
  breakdownSteps: string | null;
  variants: string | null;
  domain: string | null;
  scenario: string | null;
  frequencyTag: string | null;
}

export interface VocabExpression {
  id: string;
  englishExpr: string;
  chineseEquiv: string | null;
  contextNote: string | null;
  category: string | null;
  domain: string | null;
  scenario: string | null;
  frequencyTag: string | null;
  /** false = 习语/比喻等不可机械直译；true = 可直译；null = 未判定。 */
  literalTranslatable: boolean | null;
}

export interface DeepLearningContent {
  questionId: string;
  referenceText: string;
  comparisonNotes: string | null;
  sentencePatterns: SentencePattern[];
  vocabExpressions: VocabExpression[];
}

export interface GenerateDeepLearningContentResponse extends DeepLearningContent {
  wasCached: boolean;
}

export interface WeakPoint {
  id: string;
  category: string;
  description: string | null;
  firstDetectedAt: string;
  lastSeenAt: string;
  recurrenceCount: number;
  status: number;
  priority: number;
}

export interface ProgressSnapshot {
  id: string;
  periodStart: string;
  periodEnd: string;
  difficultyTier: string | null;
  avgBandMeaningTransfer: number | null;
  avgBandTextualNorms: number | null;
  avgBandLanguageProficiency: number | null;
  passRate: number | null;
  trendNote: string | null;
  keyTurningPoint: boolean;
}

export interface ReviewPatternItem {
  id: string;
  questionId: string | null;
  patternName: string;
  exampleSentence: string | null;
  domain: string | null;
  scenario: string | null;
  frequencyTag: string | null;
  timesEncountered: number;
  masteryLevel: number;
  lastReviewedAt: string | null;
}

export interface ReviewVocabItem {
  id: string;
  questionId: string | null;
  englishExpr: string;
  chineseEquiv: string | null;
  domain: string | null;
  scenario: string | null;
  frequencyTag: string | null;
  timesEncountered: number;
  masteryLevel: number;
  lastReviewedAt: string | null;
}

/* ------------------------------ 内容管理后台 ------------------------------ */

export interface CreateExamTypeRequest {
  code: string;
  name: string;
  subjectCategory: number;
  sourceLanguage?: string | null;
  targetLanguage?: string | null;
  gradeLevel?: string | null;
  description?: string | null;
}

/** examTypeId 不在这里——后端路由是 /exam-types/{examTypeId}/assessment-dimensions，examTypeId 是路径参数，不是 body 字段。 */
export interface CreateAssessmentDimensionRequest {
  dimensionKey: string;
  dimensionName: string;
  scaleType: number;
  passThreshold?: string | null;
  applicableTaskType?: number | null;
  levelDescriptions: string;
  rubricVersion: string;
  effectiveFrom: string;
  sourceReference?: string | null;
}

/** examTypeId 同样是路径参数（/exam-types/{examTypeId}/error-taxonomies），不放在 body 里。 */
export interface CreateErrorTaxonomyRequest {
  categoryKey: string;
  categoryName: string;
  description?: string | null;
  exampleCases?: string | null;
}

export interface PromptTemplate {
  id: string;
  examTypeId: string | null;
  subjectCategory: number | null;
  templateType: number;
  layer: number;
  templateContent: string;
  version: number;
  isActive: boolean;
  createdAt: string;
}

/** 后端要求显式传 version（CreatePromptTemplateCommand），没有像 assessment-dimensions 那样的自动递增逻辑。 */
export interface CreatePromptTemplateRequest {
  examTypeId?: string | null;
  subjectCategory?: number | null;
  templateType: number;
  layer: number;
  templateContent: string;
  version: number;
}

export interface LlmProviderModel {
  providerKey: string;
  model: string;
  label: string | null;
  isCurrent: boolean;
  createdAt: string;
}

/** 对应后端 LlmProviderResultItem——currentModel 只是模型 id 字符串（不是完整对象，要显示 label
 * 需要另外查一次 GET /llm-provider-settings/{key}/models 目录自己拼），读字段叫 extraSettings（不
 * 带 Json 后缀，写字段 UpdateLlmProviderSettingsRequest.extraSettingsJson 才带）。 */
export interface LlmProviderSettings {
  providerKey: string;
  isActive: boolean;
  currentModel: string | null;
  thinkingEnabled: boolean;
  effort: string | null;
  extraSettings: string | null;
  updatedAt: string;
}

export interface UpdateLlmProviderSettingsRequest {
  thinkingEnabled?: boolean | null;
  effort?: string | null;
  extraSettingsJson?: string | null;
}

/** 对应后端 UpdateLlmProviderSettingsResult——比 LlmProviderSettings 少 currentModel（PATCH 这个
 * 接口根本不碰 currentModel，见 LlmProviderSettingsController 的注释）。 */
export interface UpdateLlmProviderSettingsResult {
  providerKey: string;
  isActive: boolean;
  thinkingEnabled: boolean;
  effort: string | null;
  extraSettings: string | null;
  updatedAt: string;
}

/** 对应后端 ActivateLlmProviderResult——很薄，只有两个字段。 */
export interface ActivateLlmProviderResult {
  providerKey: string;
  isActive: boolean;
}

/** 对应后端 SelectLlmProviderModelResult——很薄，不是完整的 LlmProviderModel。 */
export interface SelectLlmProviderModelResult {
  providerKey: string;
  model: string;
  isCurrent: boolean;
}

export interface CreateQuestionBankCategoryRequest {
  categoryType: number;
  name: string;
  parentId?: string | null;
  description?: string | null;
}

export interface ImportSeededErrorRequest {
  positionStart: number;
  positionEnd: number;
  errorTaxonomyId: string;
  correctReferenceText: string;
  note?: string | null;
}

export interface ImportMeaningCheckpointRequest {
  checkpointText: string;
  checkpointType?: string | null;
  importance: number;
}

/** 镜像后端 ImportUserQuestionCommand：flawedTranslationText/seededErrors 是顶层平铺字段，
 * 不是嵌套在 taskB 里（后端 record 里根本没有 taskB 这一层，是响应 DTO 才嵌套）。 */
export interface ImportUserQuestionRequest {
  taskType: number;
  difficulty: number;
  title: string;
  brief?: string | null;
  sourceText: string;
  flawedTranslationText?: string | null;
  wordCount?: number | null;
  isSeedReference?: boolean;
  visibility?: number;
  createdBy?: string | null;
  meaningCheckpoints?: ImportMeaningCheckpointRequest[];
  seededErrors?: ImportSeededErrorRequest[];
}

/** 对应后端 GetUserByIdResult（GET /users/{id}）。 */
export interface UserProfile {
  id: string;
  username: string;
  email: string;
  displayName: string | null;
  createdAt: string;
  lastLoginAt: string | null;
}
