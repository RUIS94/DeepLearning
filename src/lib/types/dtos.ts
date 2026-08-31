// 与后端 record 一一对应的 DTO（camelCase JSON）

export interface ProblemDetails {
  status: number;
  title: string;
  correlationId?: string;
  errors?: Record<string, string[]>;
}

export interface ExamType {
  id: string;
  code: string;
  name: string;
  subjectCategory: number;
  sourceLanguage: string | null;
  targetLanguage: string | null;
  gradeLevel: string | null;
  description: string | null;
  isActive: boolean;
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

export interface StandardOverride {
  id: string;
  submissionId: string;
  scope: number;
  status: number;
  dimensionKey: string | null;
  reason: string;
  createdAt: string;
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

export interface CreateAssessmentDimensionRequest {
  examTypeId: string;
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

export interface CreateErrorTaxonomyRequest {
  examTypeId: string;
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

export interface CreatePromptTemplateRequest {
  examTypeId?: string | null;
  subjectCategory?: number | null;
  templateType: number;
  layer: number;
  templateContent: string;
}

export interface LlmProviderModel {
  providerKey: string;
  model: string;
  label: string | null;
  isCurrent: boolean;
  createdAt: string;
}

export interface LlmProviderSettings {
  providerKey: string;
  isActive: boolean;
  thinkingEnabled: boolean;
  effort: string | null;
  extraSettingsJson: string | null;
  currentModel: LlmProviderModel | null;
}

export interface UpdateLlmProviderSettingsRequest {
  thinkingEnabled?: boolean | null;
  effort?: string | null;
  extraSettingsJson?: string | null;
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

export interface ImportUserQuestionRequest {
  taskType: number;
  difficulty: number;
  title: string;
  brief?: string | null;
  sourceText: string;
  wordCount?: number | null;
  isSeedReference?: boolean;
  visibility?: number;
  createdBy?: string | null;
  meaningCheckpoints?: ImportMeaningCheckpointRequest[];
  taskB?: {
    flawedTranslationText: string;
    seededErrors: ImportSeededErrorRequest[];
  } | null;
}
