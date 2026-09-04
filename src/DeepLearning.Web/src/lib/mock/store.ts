/**
 * Mock 数据层（UI 原型阶段）。
 *
 * 这一层刻意模仿方案第 5 节 `lib/api/*.ts` 的函数签名与后端 DTO 形状，
 * 后续接入真实 .NET 后端时，只需要把这些函数体换成 apiFetch 调用即可，
 * 页面与组件不需要改动。
 */
import {
  Difficulty,
  FollowUpVerdict,
  MasteryLevel,
  OverrideScope,
  OverrideStatus,
  Priority,
  QuestionOrigin,
  ScaleType,
  SourceType,
  SubmissionStatus,
  TaskType,
  Visibility,
  WeakPointStatus,
} from "@/lib/types/enums";
import type {
  ActivateStandardOverrideResult,
  AssessmentDimension,
  CreateAssessmentDimensionRequest,
  CreateErrorTaxonomyRequest,
  CreateExamTypeRequest,
  CreateFollowUpQuestionRequest,
  CreatePromptTemplateRequest,
  CreateQuestionBankCategoryRequest,
  CreateQuestionResult,
  CreateSubmissionRequest,
  CreateSubmissionResult,
  DeepLearningContent,
  ErrorTaxonomy,
  ExamTypeDetail,
  FollowUpQuestionDetail,
  FollowUpQuestionResult,
  GenerateDeepLearningContentResponse,
  GenerateQuestionRequest,
  GradeSubmissionResult,
  ImportUserQuestionRequest,
  LlmProviderModel,
  LlmProviderSettings,
  ProgressSnapshot,
  PromptTemplate,
  QuestionBankCategory,
  ProblemDetails,
  QuestionDetail,
  QuestionListItem,
  ReviewPatternItem,
  ReviewVocabItem,
  SeedReferenceLink,
  StandardOverride,
  StandardOverrideDetail,
  SubmissionDetail,
  UpdateLlmProviderSettingsRequest,
  UserProfile,
  WeakPoint,
} from "@/lib/types/dtos";

export const MOCK_USER = {
  id: "11111111-1111-4111-8111-111111111111",
  email: "learner@example.com",
  name: "练习者",
};

/** 对应后端 GET /users/{id}（GetUserByIdQuery）——目前只有 MOCK_USER 这一个用户。 */
export async function getUserById(id: string): Promise<UserProfile> {
  await delay(80);
  if (id !== MOCK_USER.id)
    throw new ApiError(404, { status: 404, title: `User with id ${id} was not found.` });
  return {
    id: MOCK_USER.id,
    username: MOCK_USER.email,
    email: MOCK_USER.email,
    displayName: MOCK_USER.name,
    createdAt: "2026-01-08T02:00:00Z",
    lastLoginAt: new Date().toISOString(),
  };
}

export const delay = (ms: number) => new Promise((r) => setTimeout(r, ms));

// The seeded mock dataset already contains IDs such as q-1001, so generated
// records must begin above that range to avoid collisions in React list keys.
let seq = 2000;
const nextId = (prefix: string) => `${prefix}-${++seq}`;

/* ----------------------------- 考试配置 ----------------------------- */

export const examType: ExamTypeDetail = {
  id: "exam-naati-ct",
  code: "NAATI_CT",
  name: "NAATI 认证笔译（中英）",
  subjectCategory: 0,
  sourceLanguage: "en",
  targetLanguage: "zh",
  gradeLevel: "Certified Translator",
  description: "澳大利亚 NAATI 认证笔译考试，含 TaskA 翻译与 TaskB 找错标注两类任务。",
  isActive: true,
  createdAt: "2026-01-08T02:00:00Z",
};

export const dimensions: AssessmentDimension[] = [
  {
    id: "dim-1",
    examTypeId: examType.id,
    dimensionKey: "meaning_transfer",
    dimensionName: "意义传递",
    scaleType: ScaleType.band_1_5,
    passThreshold: "Band 2",
    applicableTaskType: null,
    levelDescriptions: "Band 1 完整准确；Band 5 严重扭曲原意",
    rubricVersion: "v2026.1",
    effectiveFrom: "2026-01-01",
    effectiveTo: null,
    sourceReference: "NAATI Rubric 2026",
    verifiedAt: "2026-01-10T00:00:00Z",
  },
  {
    id: "dim-2",
    examTypeId: examType.id,
    dimensionKey: "textual_norms",
    dimensionName: "语篇规范",
    scaleType: ScaleType.band_1_5,
    passThreshold: "Band 2",
    applicableTaskType: null,
    levelDescriptions: "语域、体例、格式与目标语读者习惯的契合度",
    rubricVersion: "v2026.1",
    effectiveFrom: "2026-01-01",
    effectiveTo: null,
    sourceReference: "NAATI Rubric 2026",
    verifiedAt: "2026-01-10T00:00:00Z",
  },
  {
    id: "dim-3",
    examTypeId: examType.id,
    dimensionKey: "language_proficiency",
    dimensionName: "语言能力",
    scaleType: ScaleType.band_1_5,
    passThreshold: "Band 2",
    applicableTaskType: null,
    levelDescriptions: "语法、搭配、标点与行文流畅度",
    rubricVersion: "v2026.1",
    effectiveFrom: "2026-01-01",
    effectiveTo: null,
    sourceReference: "NAATI Rubric 2026",
    verifiedAt: "2026-01-10T00:00:00Z",
  },
];

export const errorTaxonomies: ErrorTaxonomy[] = [
  {
    id: "tax-1",
    examTypeId: examType.id,
    categoryKey: "distortion",
    categoryName: "意义扭曲",
    description: "译文与原文意义不符",
    exampleCases: null,
  },
  {
    id: "tax-2",
    examTypeId: examType.id,
    categoryKey: "omission",
    categoryName: "漏译",
    description: "原文信息缺失",
    exampleCases: null,
  },
  {
    id: "tax-3",
    examTypeId: examType.id,
    categoryKey: "addition",
    categoryName: "增译",
    description: "添加原文没有的信息",
    exampleCases: null,
  },
  {
    id: "tax-4",
    examTypeId: examType.id,
    categoryKey: "register",
    categoryName: "语域不当",
    description: "文体或正式程度不符",
    exampleCases: null,
  },
  {
    id: "tax-5",
    examTypeId: examType.id,
    categoryKey: "terminology",
    categoryName: "术语错误",
    description: "专有名词或行业术语译法错误",
    exampleCases: null,
  },
  {
    id: "tax-6",
    examTypeId: examType.id,
    categoryKey: "grammar",
    categoryName: "语法搭配",
    description: "语法、搭配或标点问题",
    exampleCases: null,
  },
];

export const categories: QuestionBankCategory[] = [
  { id: "cat-health", categoryType: 0, name: "医疗健康", parentId: null, description: null },
  { id: "cat-legal", categoryType: 0, name: "法律政务", parentId: null, description: null },
  { id: "cat-business", categoryType: 0, name: "商务财经", parentId: null, description: null },
  { id: "cat-notice", categoryType: 1, name: "公告通知", parentId: null, description: null },
  { id: "cat-letter", categoryType: 1, name: "信函往来", parentId: null, description: null },
];

/* ------------------------------- 题目 ------------------------------- */

const flawedA = `尊敬的居民：市政厅将于下周一起对中央大街进行为期三周的道路维修。施工期间，公交 12 路和 30 路将改道行驶，请提前规划出行路线。若您有紧急医疗需求，请拨打市政服务热线，我们会安排优先通道。感谢您的耐心与配合。`;

function span(text: string, needle: string) {
  const start = text.indexOf(needle);
  return { positionStart: start, positionEnd: start + needle.length };
}

const questions: QuestionDetail[] = [
  {
    id: "q-1001",
    taskType: TaskType.A,
    difficulty: Difficulty.medium,
    title: "社区健康中心疫苗接种通知",
    brief: "公共卫生类通知，注意语域与面向公众的可读性。",
    sourceText: `The Community Health Centre will offer free influenza vaccinations to all residents aged 65 and over from 6 May. Appointments are strongly recommended, although a limited number of walk-in slots will be available each morning. Residents who are currently unwell, or who have had a fever in the past 48 hours, should postpone their appointment and contact the centre by telephone. Carers accompanying eligible residents may also receive a vaccination at no cost.`,
    wordCount: 74,
    origin: QuestionOrigin.real_exam_seed,
    sourceType: SourceType.real_exam,
    isSeedReference: true,
    inBank: true,
    visibility: Visibility.Shared,
    createdBy: null,
    createdAt: "2026-03-02T09:00:00Z",
    isActive: true,
    meaningCheckpoints: [
      {
        id: "mc-1",
        checkpointText: "65 岁及以上居民免费接种",
        checkpointType: "fact",
        importance: 0,
      },
      {
        id: "mc-2",
        checkpointText: "建议预约，但每天上午有少量现场名额",
        checkpointType: "fact",
        importance: 0,
      },
      {
        id: "mc-3",
        checkpointText: "48 小时内发热者需推迟并电话联系",
        checkpointType: "condition",
        importance: 0,
      },
      {
        id: "mc-4",
        checkpointText: "陪同的照护者亦可免费接种",
        checkpointType: "fact",
        importance: 1,
      },
    ],
    taskB: null,
    categoryIds: ["cat-health", "cat-notice"],
  },
  {
    id: "q-1002",
    taskType: TaskType.A,
    difficulty: Difficulty.hard,
    title: "租赁纠纷调解结果函",
    brief: "法律语域，注意称谓、条款编号与正式程度。",
    sourceText: `Following the conciliation conference held on 12 March, the Tribunal notes that the parties have reached an agreement in principle regarding the outstanding rent of $2,340. The tenant undertakes to pay this amount in six equal monthly instalments, commencing 1 April. Should the tenant default on any instalment, the landlord may apply to reinstate the original application without paying a further filing fee.`,
    wordCount: 63,
    origin: QuestionOrigin.ai_generated,
    sourceType: SourceType.ai_generated,
    isSeedReference: false,
    inBank: true,
    visibility: Visibility.Shared,
    createdBy: null,
    createdAt: "2026-04-11T09:00:00Z",
    isActive: true,
    meaningCheckpoints: [
      {
        id: "mc-5",
        checkpointText: "3 月 12 日调解会议后达成原则性协议",
        checkpointType: "fact",
        importance: 0,
      },
      {
        id: "mc-6",
        checkpointText: "欠租 2,340 澳元分六期等额偿还",
        checkpointType: "fact",
        importance: 0,
      },
      {
        id: "mc-7",
        checkpointText: "违约时房东可免费恢复原申请",
        checkpointType: "condition",
        importance: 0,
      },
    ],
    taskB: null,
    categoryIds: ["cat-legal", "cat-letter"],
  },
  {
    id: "q-1003",
    taskType: TaskType.A,
    difficulty: Difficulty.easy,
    title: "学校家长会安排邮件",
    brief: "日常校园沟通，语言平实，重点在信息完整。",
    sourceText: `Dear Parents and Carers, our mid-year parent-teacher interviews will take place on Thursday 22 June between 3:30 pm and 7:00 pm in the school hall. Bookings open online this Friday and each interview is limited to ten minutes. If you require an interpreter, please tick the relevant box when booking so that we can arrange one in advance.`,
    wordCount: 58,
    origin: QuestionOrigin.ai_generated,
    sourceType: SourceType.ai_generated,
    isSeedReference: false,
    inBank: true,
    visibility: Visibility.Shared,
    createdBy: null,
    createdAt: "2026-05-20T09:00:00Z",
    isActive: true,
    meaningCheckpoints: [
      {
        id: "mc-8",
        checkpointText: "6 月 22 日周四 15:30–19:00 学校礼堂",
        checkpointType: "fact",
        importance: 0,
      },
      {
        id: "mc-9",
        checkpointText: "每场限时十分钟，周五开放线上预约",
        checkpointType: "fact",
        importance: 0,
      },
      {
        id: "mc-10",
        checkpointText: "需要口译需在预约时勾选",
        checkpointType: "condition",
        importance: 1,
      },
    ],
    taskB: null,
    categoryIds: ["cat-notice"],
  },
  {
    id: "q-1004",
    taskType: TaskType.B,
    difficulty: Difficulty.medium,
    title: "市政道路维修通知（找错）",
    brief: "译文中植入了若干错误，请标注位置、选择错误类型并给出修改。",
    sourceText: `Dear Residents, the City Council will carry out road resurfacing works on Central Avenue for four weeks, starting next Monday. During the works, bus routes 12 and 30 will be diverted, so please plan your journey in advance. If you have urgent medical needs, call the Council service line and we will arrange priority access. Thank you for your patience and cooperation.`,
    wordCount: 62,
    origin: QuestionOrigin.real_exam_seed,
    sourceType: SourceType.real_exam,
    isSeedReference: true,
    inBank: true,
    visibility: Visibility.Shared,
    createdBy: null,
    createdAt: "2026-04-28T09:00:00Z",
    isActive: true,
    meaningCheckpoints: [],
    taskB: {
      flawedTranslationText: flawedA,
      seededErrors: [
        {
          id: "se-1",
          ...span(flawedA, "为期三周"),
          errorTaxonomyId: "tax-1",
          errorCategoryKey: "distortion",
          correctReferenceText: "为期四周",
          note: "原文为 four weeks。",
        },
        {
          id: "se-2",
          ...span(flawedA, "市政厅"),
          errorTaxonomyId: "tax-5",
          errorCategoryKey: "terminology",
          correctReferenceText: "市议会",
          note: "City Council 应译为市议会/市政委员会。",
        },
        {
          id: "se-3",
          ...span(flawedA, "我们会安排优先通道"),
          errorTaxonomyId: "tax-4",
          errorCategoryKey: "register",
          correctReferenceText: "我们将为您安排优先通行安排",
          note: "面向公众通知的语域略显口语。",
        },
      ],
    },
    categoryIds: ["cat-notice", "cat-legal"],
  },
  {
    id: "q-1005",
    taskType: TaskType.A,
    difficulty: Difficulty.hard,
    title: "银行账户异常交易告知书",
    brief: "金融语域，术语精确度要求高。",
    sourceText: `We are writing to advise you that unusual activity has been detected on your transaction account ending in 4821. As a precaution, the card linked to this account has been temporarily blocked and a replacement will be posted to your registered address within five business days. You will not be held liable for any unauthorised transactions provided you notify us within thirty days of the statement date.`,
    wordCount: 66,
    origin: QuestionOrigin.ai_generated,
    sourceType: SourceType.ai_generated,
    isSeedReference: false,
    inBank: false,
    visibility: Visibility.Private,
    createdBy: MOCK_USER.id,
    createdAt: "2026-06-14T09:00:00Z",
    isActive: true,
    meaningCheckpoints: [
      {
        id: "mc-11",
        checkpointText: "尾号 4821 账户检测到异常交易",
        checkpointType: "fact",
        importance: 0,
      },
      {
        id: "mc-12",
        checkpointText: "卡片已临时冻结，五个工作日内寄送新卡",
        checkpointType: "fact",
        importance: 0,
      },
      {
        id: "mc-13",
        checkpointText: "对账单日起三十天内通知即免责",
        checkpointType: "condition",
        importance: 0,
      },
    ],
    taskB: null,
    categoryIds: ["cat-business", "cat-letter"],
  },
];

/* ------------------------------ 内存状态 ------------------------------ */

const submissions = new Map<string, SubmissionDetail>();
/** 存 createFollowUp 累积的完整记录，按后端 FollowUpQuestionResultItem 的形状投影给 listFollowUps/getFollowUpQuestionById。 */
const followUps = new Map<string, FollowUpQuestionDetail[]>();
const deepLearningCache = new Map<string, DeepLearningContent>();
/** 内部存完整的 StandardOverrideDetail（含 originalRuleText/triggeredByFollowupId），
 * listStandardOverrides 只投影出列表项需要的字段，与后端 List/GetById 返回不同形状一致。 */
const overrides: StandardOverrideDetail[] = [
  {
    id: "ovr-1",
    scope: OverrideScope.grading_rubric,
    dimensionOrRule: "textual_norms",
    originalRuleText: "「敬启者」等称谓一律按语域不当扣分。",
    revisedRuleText: "「敬启者」在澳洲政府公文语境下属可接受的正式称谓，不再对该称谓单独扣分。",
    triggeredByFollowupId: "fu-9001",
    status: OverrideStatus.active,
    previousOverrideId: null,
    effectiveFrom: "2026-07-02T05:12:00Z",
    createdAt: "2026-07-02T05:12:00Z",
  },
  {
    id: "ovr-2",
    scope: OverrideScope.translation_reference,
    dimensionOrRule: "meaning_transfer",
    originalRuleText: "“walk-in slots” 统一译为「无需预约名额」。",
    revisedRuleText: "“walk-in slots” 亦可接受译为「现场号」，累计 2 次同类反馈后生效。",
    triggeredByFollowupId: "fu-9002",
    status: OverrideStatus.observing,
    previousOverrideId: null,
    effectiveFrom: null,
    createdAt: "2026-08-15T11:40:00Z",
  },
];

/** 对应后端 SeedReferenceLinkResultItem——记录 AI 出题时参考了哪些真题种子，按 questionId 索引。 */
const seedReferenceLinks = new Map<string, SeedReferenceLink[]>([
  [
    "q-1002",
    [
      {
        id: "srl-1",
        seedQuestionId: "q-1001",
        seedQuestionTitle: "社区健康中心疫苗接种通知",
        similarityReason: "同属公共服务类公告，语域与句式结构相近。",
        createdAt: "2026-04-11T09:00:00Z",
      },
    ],
  ],
  [
    "q-1003",
    [
      {
        id: "srl-2",
        seedQuestionId: "q-1004",
        seedQuestionTitle: "市政道路维修通知（找错）",
        similarityReason: "同为面向公众的市政通知，条件句式可复用。",
        createdAt: "2026-05-20T09:00:00Z",
      },
    ],
  ],
]);

/* ------------------------------ 只读查询 ------------------------------ */

export async function getExamConfig() {
  await delay(120);
  return { examType, dimensions, errorTaxonomies };
}

export async function listCategories() {
  await delay(80);
  return categories;
}

/* --------------------------- 内容管理后台（Admin） --------------------------- */

const examTypes: ExamTypeDetail[] = [examType];

export async function listExamTypes(): Promise<ExamTypeDetail[]> {
  await delay(100);
  return [...examTypes];
}

export async function createExamType(req: CreateExamTypeRequest): Promise<ExamTypeDetail> {
  await delay(200);
  const created: ExamTypeDetail = {
    id: nextId("exam"),
    code: req.code,
    name: req.name,
    subjectCategory: req.subjectCategory,
    sourceLanguage: req.sourceLanguage ?? null,
    targetLanguage: req.targetLanguage ?? null,
    gradeLevel: req.gradeLevel ?? null,
    description: req.description ?? null,
    isActive: true,
    createdAt: new Date().toISOString(),
  };
  examTypes.push(created);
  return created;
}

export async function listAssessmentDimensions(examTypeId: string): Promise<AssessmentDimension[]> {
  await delay(100);
  const now = new Date().toISOString();
  return dimensions.filter(
    (d) =>
      d.examTypeId === examTypeId &&
      d.effectiveFrom <= now &&
      (d.effectiveTo === null || d.effectiveTo > now),
  );
}

/** 新建评分维度版本：语义上是“修订生效日期”，自动关闭上一条仍生效的记录（方案 3.8 节）。
 * examTypeId 是单独的参数，不在 req 里——镜像后端路由 /exam-types/{examTypeId}/assessment-dimensions
 * 把 examTypeId 当路径参数而非 body 字段。 */
export async function createAssessmentDimension(
  examTypeId: string,
  req: CreateAssessmentDimensionRequest,
): Promise<AssessmentDimension> {
  await delay(200);
  const current = dimensions.find(
    (d) =>
      d.examTypeId === examTypeId && d.dimensionKey === req.dimensionKey && d.effectiveTo === null,
  );
  if (current && req.effectiveFrom <= current.effectiveFrom) {
    throw new ApiError(400, {
      status: 400,
      title: "New version's effectiveFrom must be after the current version's.",
      errors: { effectiveFrom: ["必须晚于当前生效版本的 effectiveFrom。"] },
    });
  }
  const created: AssessmentDimension = {
    id: nextId("dim"),
    examTypeId,
    dimensionKey: req.dimensionKey,
    dimensionName: req.dimensionName,
    scaleType: req.scaleType,
    passThreshold: req.passThreshold ?? null,
    applicableTaskType: req.applicableTaskType ?? null,
    levelDescriptions: req.levelDescriptions,
    rubricVersion: req.rubricVersion,
    effectiveFrom: req.effectiveFrom,
    effectiveTo: null,
    sourceReference: req.sourceReference ?? null,
    verifiedAt: null,
  };
  if (current) current.effectiveTo = req.effectiveFrom;
  dimensions.push(created);
  return created;
}

export async function listErrorTaxonomiesByExamType(examTypeId: string): Promise<ErrorTaxonomy[]> {
  await delay(100);
  return errorTaxonomies.filter((t) => t.examTypeId === examTypeId);
}

/** examTypeId 是单独的参数——镜像后端路由 /exam-types/{examTypeId}/error-taxonomies。 */
export async function createErrorTaxonomy(
  examTypeId: string,
  req: CreateErrorTaxonomyRequest,
): Promise<ErrorTaxonomy> {
  await delay(200);
  const created: ErrorTaxonomy = {
    id: nextId("tax"),
    examTypeId,
    categoryKey: req.categoryKey,
    categoryName: req.categoryName,
    description: req.description ?? null,
    exampleCases: req.exampleCases ?? null,
  };
  errorTaxonomies.push(created);
  return created;
}

const promptTemplates: PromptTemplate[] = [
  {
    id: "pt-1",
    examTypeId: null,
    subjectCategory: 0,
    templateType: 0,
    layer: 0,
    templateContent:
      "You are generating a {{ task_type }} exam question... Output ONLY the JSON shape described below.",
    version: 1,
    isActive: true,
    createdAt: "2026-02-01T00:00:00Z",
  },
  {
    id: "pt-2",
    examTypeId: examType.id,
    subjectCategory: null,
    templateType: 1,
    layer: 1,
    templateContent:
      "Grade the submission strictly against {{ dimension.dimension_name }} using the Band descriptions below...",
    version: 3,
    isActive: true,
    createdAt: "2026-01-15T00:00:00Z",
  },
];

export async function listPromptTemplates(filter?: {
  examTypeId?: string | undefined;
  subjectCategory?: number | undefined;
  templateType?: number | undefined;
}): Promise<PromptTemplate[]> {
  await delay(120);
  return promptTemplates
    .filter((t) => (filter?.examTypeId ? t.examTypeId === filter.examTypeId : true))
    .filter((t) =>
      filter?.subjectCategory === undefined ? true : t.subjectCategory === filter.subjectCategory,
    )
    .filter((t) =>
      filter?.templateType === undefined ? true : t.templateType === filter.templateType,
    );
}

/** version 必须由调用方显式传入——后端 CreatePromptTemplateCommand 要求显式 int Version，
 * 没有像 assessment-dimensions 那样按"同族记录数+1"自动递增的逻辑，之前这里客户端自算版本号
 * 是错的，接真实后端会导致版本号与后端语义脱节（静默写入错误数据，不会报错）。 */
export async function createPromptTemplate(
  req: CreatePromptTemplateRequest,
): Promise<PromptTemplate> {
  await delay(200);
  const created: PromptTemplate = {
    id: nextId("pt"),
    examTypeId: req.examTypeId ?? null,
    subjectCategory: req.subjectCategory ?? null,
    templateType: req.templateType,
    layer: req.layer,
    templateContent: req.templateContent,
    version: req.version,
    isActive: true,
    createdAt: new Date().toISOString(),
  };
  promptTemplates.push(created);
  return created;
}

const llmProviderModels: LlmProviderModel[] = [
  {
    providerKey: "claude",
    model: "claude-opus-5",
    label: "Opus 5",
    isCurrent: true,
    createdAt: "2026-01-01T00:00:00Z",
  },
  {
    providerKey: "claude",
    model: "claude-sonnet-5",
    label: "Sonnet 5",
    isCurrent: false,
    createdAt: "2026-01-01T00:00:00Z",
  },
  {
    providerKey: "openai",
    model: "gpt-4o-mini",
    label: null,
    isCurrent: true,
    createdAt: "2026-01-01T00:00:00Z",
  },
  {
    providerKey: "deepseek",
    model: "deepseek-v4-flash",
    label: null,
    isCurrent: true,
    createdAt: "2026-01-01T00:00:00Z",
  },
  {
    providerKey: "mimo",
    model: "mimo-v2.5-pro",
    label: null,
    isCurrent: true,
    createdAt: "2026-01-01T00:00:00Z",
  },
];

/** 内部存储字段名与后端读接口一致：extraSettings（不带 Json 后缀）+ updatedAt。
 * currentModel 不存在这里——它是从 llmProviderModels 目录里 isCurrent=true 的那条派生出来的
 * 只读字符串（后端 LlmProviderResultItem.CurrentModel 同理），不是这张表自己的字段。 */
const llmProviderSettingsList: Omit<LlmProviderSettings, "currentModel">[] = [
  {
    providerKey: "claude",
    isActive: false,
    thinkingEnabled: true,
    effort: "medium",
    extraSettings: null,
    updatedAt: "2026-01-01T00:00:00Z",
  },
  {
    providerKey: "openai",
    isActive: false,
    thinkingEnabled: false,
    effort: null,
    extraSettings: null,
    updatedAt: "2026-01-01T00:00:00Z",
  },
  {
    providerKey: "deepseek",
    isActive: false,
    thinkingEnabled: false,
    effort: null,
    extraSettings: null,
    updatedAt: "2026-01-01T00:00:00Z",
  },
  {
    providerKey: "mimo",
    isActive: true,
    thinkingEnabled: false,
    effort: null,
    extraSettings: null,
    updatedAt: "2026-01-01T00:00:00Z",
  },
];

function withCurrentModel(s: Omit<LlmProviderSettings, "currentModel">): LlmProviderSettings {
  return {
    ...s,
    currentModel:
      llmProviderModels.find((m) => m.providerKey === s.providerKey && m.isCurrent)?.model ?? null,
  };
}

export async function listLlmProviderSettings(): Promise<LlmProviderSettings[]> {
  await delay(120);
  return llmProviderSettingsList.map(withCurrentModel);
}

export async function updateLlmProviderSettings(
  providerKey: string,
  patch: UpdateLlmProviderSettingsRequest,
): Promise<LlmProviderSettings> {
  await delay(200);
  const row = llmProviderSettingsList.find((s) => s.providerKey === providerKey);
  if (!row)
    throw new ApiError(404, { status: 404, title: `Provider '${providerKey}' was not found.` });
  if (patch.thinkingEnabled !== undefined && patch.thinkingEnabled !== null)
    row.thinkingEnabled = patch.thinkingEnabled;
  if (patch.effort !== undefined) row.effort = patch.effort;
  if (patch.extraSettingsJson !== undefined) row.extraSettings = patch.extraSettingsJson;
  row.updatedAt = new Date().toISOString();
  return withCurrentModel(row);
}

export async function activateLlmProvider(providerKey: string): Promise<LlmProviderSettings> {
  await delay(200);
  const row = llmProviderSettingsList.find((s) => s.providerKey === providerKey);
  if (!row)
    throw new ApiError(404, { status: 404, title: `Provider '${providerKey}' was not found.` });
  llmProviderSettingsList.forEach((s) => (s.isActive = s.providerKey === providerKey));
  row.updatedAt = new Date().toISOString();
  return withCurrentModel(row);
}

export async function listLlmProviderModels(providerKey: string): Promise<LlmProviderModel[]> {
  await delay(100);
  return llmProviderModels.filter((m) => m.providerKey === providerKey);
}

export async function addLlmProviderModel(
  providerKey: string,
  model: string,
  label?: string | null,
): Promise<LlmProviderModel> {
  await delay(200);
  if (llmProviderModels.some((m) => m.providerKey === providerKey && m.model === model)) {
    throw new ApiError(409, {
      status: 409,
      title: `Model '${model}' is already cataloged for provider '${providerKey}'.`,
    });
  }
  const created: LlmProviderModel = {
    providerKey,
    model,
    label: label ?? null,
    isCurrent: false,
    createdAt: new Date().toISOString(),
  };
  llmProviderModels.push(created);
  return created;
}

export async function selectLlmProviderModel(
  providerKey: string,
  model: string,
): Promise<LlmProviderModel> {
  await delay(200);
  const target = llmProviderModels.find((m) => m.providerKey === providerKey && m.model === model);
  if (!target)
    throw new ApiError(404, {
      status: 404,
      title: `Model '${model}' is not cataloged for provider '${providerKey}' yet.`,
    });
  llmProviderModels.forEach((m) => {
    if (m.providerKey === providerKey) m.isCurrent = m === target;
  });
  return target;
}

export async function createQuestionBankCategory(
  req: CreateQuestionBankCategoryRequest,
): Promise<QuestionBankCategory> {
  await delay(200);
  const created: QuestionBankCategory = {
    id: nextId("cat"),
    categoryType: req.categoryType,
    name: req.name,
    parentId: req.parentId ?? null,
    description: req.description ?? null,
  };
  categories.push(created);
  return created;
}

export async function tagQuestionWithCategory(categoryId: string, questionId: string) {
  await delay(150);
  const question = questions.find((q) => q.id === questionId);
  if (!question)
    throw new ApiError(404, {
      status: 404,
      title: `Question with id ${questionId} was not found.`,
    });
  if (!question.categoryIds.includes(categoryId)) question.categoryIds.push(categoryId);
  return question;
}

export async function importUserQuestion(
  req: ImportUserQuestionRequest,
): Promise<CreateQuestionResult> {
  await delay(250);
  const created: QuestionDetail = {
    id: nextId("q"),
    taskType: req.taskType,
    difficulty: req.difficulty,
    title: req.title,
    brief: req.brief ?? null,
    sourceText: req.sourceText,
    wordCount: req.wordCount ?? req.sourceText.split(/\s+/).filter(Boolean).length,
    origin: QuestionOrigin.user_uploaded,
    sourceType: SourceType.user_generated,
    isSeedReference: req.isSeedReference ?? false,
    inBank: true,
    visibility: req.visibility ?? Visibility.Private,
    createdBy: req.createdBy ?? MOCK_USER.id,
    createdAt: new Date().toISOString(),
    isActive: true,
    meaningCheckpoints: (req.meaningCheckpoints ?? []).map((c) => ({
      id: nextId("mc"),
      checkpointText: c.checkpointText,
      checkpointType: c.checkpointType ?? null,
      importance: c.importance,
    })),
    // 镜像后端 ImportUserQuestionCommand：flawedTranslationText/seededErrors 是顶层平铺字段。
    taskB: req.flawedTranslationText
      ? {
          flawedTranslationText: req.flawedTranslationText,
          seededErrors: (req.seededErrors ?? []).map((e) => ({
            id: nextId("se"),
            positionStart: e.positionStart,
            positionEnd: e.positionEnd,
            errorTaxonomyId: e.errorTaxonomyId,
            errorCategoryKey:
              errorTaxonomies.find((t) => t.id === e.errorTaxonomyId)?.categoryKey ?? "",
            correctReferenceText: e.correctReferenceText,
            note: e.note ?? null,
          })),
        }
      : null,
    categoryIds: [],
  };
  questions.unshift(created);
  // 对应后端 ImportUserQuestionResult——很薄，完整详情要另外 GET /questions/{id}。
  return {
    id: created.id,
    taskType: created.taskType,
    difficulty: created.difficulty,
    title: created.title,
    createdAt: created.createdAt,
  };
}

export async function listQuestions(filter?: {
  taskType?: number | undefined;
  difficulty?: number | undefined;
  categoryId?: string | undefined;
  inBank?: boolean | undefined;
}): Promise<QuestionListItem[]> {
  await delay(160);
  return questions
    .filter((q) => (filter?.taskType === undefined ? true : q.taskType === filter.taskType))
    .filter((q) => (filter?.difficulty === undefined ? true : q.difficulty === filter.difficulty))
    .filter((q) => (filter?.categoryId ? q.categoryIds.includes(filter.categoryId) : true))
    .filter((q) => (filter?.inBank === undefined ? true : q.inBank === filter.inBank))
    .map((q) => ({
      id: q.id,
      taskType: q.taskType,
      difficulty: q.difficulty,
      title: q.title,
      wordCount: q.wordCount,
      inBank: q.inBank,
      createdAt: q.createdAt,
      myAttemptCount: 0,
      myLatestSubmissionId: null,
    }));
}

export async function getQuestionById(id: string): Promise<QuestionDetail> {
  await delay(140);
  const q = questions.find((item) => item.id === id);
  if (!q) throw new ApiError(404, { status: 404, title: `Question with id ${id} was not found.` });
  return q;
}

export async function generateQuestion(
  req: GenerateQuestionRequest,
): Promise<CreateQuestionResult> {
  await delay(2600);
  const base = questions.find((q) => q.taskType === req.taskType) ?? questions[0]!;
  const generated: QuestionDetail = {
    ...base,
    id: nextId("q"),
    title: `${req.targetWeakPoints ? "薄弱点定向 · " : "AI 生成 · "}${base.title}`,
    brief: req.targetWeakPoints
      ? "本题针对你的高优先级薄弱点（长句拆分、语域一致性）生成。"
      : "由 AI 依据所选难度与分类生成的新题目。",
    difficulty: req.difficulty ?? base.difficulty,
    origin: QuestionOrigin.ai_generated,
    sourceType: SourceType.ai_generated,
    isSeedReference: false,
    inBank: false,
    createdAt: new Date().toISOString(),
    categoryIds: req.categoryId ? [req.categoryId] : base.categoryIds,
  };
  questions.unshift(generated);
  if (req.seedQuestionIds?.length) {
    seedReferenceLinks.set(
      generated.id,
      req.seedQuestionIds.map((seedId) => ({
        id: nextId("srl"),
        seedQuestionId: seedId,
        seedQuestionTitle: questions.find((q) => q.id === seedId)?.title ?? seedId,
        similarityReason: "由调用方在出题请求中显式指定为参考真题。",
        createdAt: generated.createdAt,
      })),
    );
  }
  // 对应后端 GenerateQuestionResult——很薄，完整详情要另外 GET /questions/{id}。
  return {
    id: generated.id,
    taskType: generated.taskType,
    difficulty: generated.difficulty,
    title: generated.title,
    createdAt: generated.createdAt,
  };
}

/** 对应后端 GET /questions/{id}/seed-references——design doc §11.2 Step 8 的真题溯源读接口。 */
export async function listSeedReferences(questionId: string): Promise<SeedReferenceLink[]> {
  await delay(100);
  return seedReferenceLinks.get(questionId) ?? [];
}

/* ------------------------------ 提交与评分 ------------------------------ */

export async function createSubmission(
  req: CreateSubmissionRequest,
): Promise<CreateSubmissionResult> {
  await delay(400);
  const now = new Date().toISOString();
  const submission: SubmissionDetail = {
    id: nextId("sub"),
    questionId: req.questionId,
    userId: req.userId,
    taskType: req.taskType,
    content: req.content,
    status: SubmissionStatus.submitted,
    // 还没评判，所以不适用——评判完成后后端才会写入进度。
    weakPointGenerationStatus: null,
    submittedAt: now,
    createdAt: now,
    gradingResults: [],
    errorList: [],
    overallSummary: null,
  };
  submissions.set(submission.id, submission);
  // 对应后端 CreateSubmissionResult——刻意很薄，完整详情要另外 GET /submissions/{id}。
  return {
    id: submission.id,
    questionId: submission.questionId,
    taskType: submission.taskType,
    status: submission.status,
    submittedAt: submission.submittedAt,
  };
}

export async function getSubmissionById(id: string): Promise<SubmissionDetail> {
  await delay(120);
  const s = submissions.get(id);
  if (!s)
    throw new ApiError(404, { status: 404, title: `Submission with id ${id} was not found.` });
  return s;
}

/** examTypeId 镜像后端 POST /submissions/{id}/grade 的 GradeSubmissionRequest.examTypeId——
 * mock 逻辑本身不需要它（题库只有一个 examType），但签名要和真实接口一致，接后端时才不用改调用点。 */
export async function gradeSubmission(
  id: string,
  examTypeId: string,
): Promise<GradeSubmissionResult> {
  void examTypeId;
  await delay(3200);
  const s = submissions.get(id);
  if (!s)
    throw new ApiError(404, { status: 404, title: `Submission with id ${id} was not found.` });
  const question = questions.find((q) => q.id === s.questionId);
  const graded: SubmissionDetail = {
    ...s,
    status: SubmissionStatus.graded,
    gradingResults: [
      {
        id: nextId("gr"),
        dimensionKey: "meaning_transfer",
        dimensionName: "意义传递",
        rubricVersion: "v2026.1",
        band: 2,
        passBool: true,
        rationale:
          "核心信息点基本完整，条件类信息（时间限制、资格范围）传达准确；个别限定语处理略松，未影响整体意义。",
        cumulativeDensityFlag: false,
        cumulativeDensityNote: null,
        estimatedPassProbability: 0.9,
        confidence: "high",
        alternativeBand: 2,
      },
      {
        id: nextId("gr"),
        dimensionKey: "textual_norms",
        dimensionName: "语篇规范",
        rubricVersion: "v2026.1",
        band: 3,
        passBool: false,
        rationale:
          "公告体例把握不稳：称谓与落款风格不统一，部分句子沿用英文语序，读者阅读成本偏高。",
        cumulativeDensityFlag: true,
        cumulativeDensityNote: "同类语域问题近 5 次提交中出现 4 次，已达密度提示阈值。",
        estimatedPassProbability: 0.25,
        confidence: "medium",
        alternativeBand: 2,
      },
      {
        id: nextId("gr"),
        dimensionKey: "language_proficiency",
        dimensionName: "语言能力",
        rubricVersion: "v2026.1",
        band: 2,
        passBool: true,
        rationale: "语法与搭配整体可靠，标点使用符合中文规范，长句拆分仍可更自然。",
        cumulativeDensityFlag: false,
        cumulativeDensityNote: null,
        estimatedPassProbability: 0.62,
        confidence: "low",
        alternativeBand: 3,
      },
    ],
    errorList: [
      {
        id: nextId("err"),
        positionRef: "0:24",
        sourceTextSnippet: question?.sourceText.slice(0, 48) ?? null,
        userTextSnippet: "本中心将向所有 65 岁以上居民提供……",
        errorCategory: "distortion",
        dimensionKey: "meaning_transfer",
        severity: 2,
        summary: "数值边界偏移",
        explanation: "“aged 65 and over” 含 65 岁本身，译作「65 岁以上」在中文里通常不含本数。",
        suggestion: "改为「65 岁及以上」。",
      },
      {
        id: nextId("err"),
        positionRef: "96:132",
        sourceTextSnippet: "Appointments are strongly recommended",
        userTextSnippet: "我们非常推荐大家来预约",
        errorCategory: "register",
        dimensionKey: "textual_norms",
        severity: 1,
        summary: "语域偏口语",
        explanation: "官方通知语域应克制，口语化表达降低文本的正式度。",
        suggestion: "改为「建议提前预约」。",
      },
      {
        id: nextId("err"),
        positionRef: "210:246",
        sourceTextSnippet: "should postpone their appointment",
        userTextSnippet: "应该推迟",
        errorCategory: "omission",
        dimensionKey: "meaning_transfer",
        severity: 1,
        summary: "次要信息缺失",
        explanation: "漏译 “and contact the centre by telephone” 的联系要求。",
        suggestion: "补上「并致电本中心」。",
      },
    ],
    overallSummary: {
      overallPassProbability: 0.23,
      overallPassBool: false,
      cumulativeDensityFlag: true,
      cumulativeDensityNote:
        "meaning_transfer 维度多处轻中度问题叠加，累积密度已构成独立的降级风险。",
      conclusionText: null,
    },
  };
  submissions.set(id, graded);
  // 对应后端 GradeSubmissionResult——只有计数，评分结果需要调用方再 GET 一次 /submissions/{id}
  // 才能拿到（真实流程是"评分 → 再 GET 一次"两步，不是一次调用直接拿到结果）。
  return {
    submissionId: graded.id,
    status: graded.status,
    gradingResultCount: graded.gradingResults.length,
    errorListCount: graded.errorList.length,
  };
}

/* -------------------------------- 追问 -------------------------------- */

/** 对应后端 GET /follow-ups?submissionId=（ListFollowUpQuestionsQuery），按 createdAt 升序。 */
export async function listFollowUps(submissionId: string): Promise<FollowUpQuestionDetail[]> {
  await delay(100);
  return followUps.get(submissionId) ?? [];
}

/** 对应后端 GET /follow-ups/{id}（GetFollowUpQuestionByIdQuery）。 */
export async function getFollowUpQuestionById(id: string): Promise<FollowUpQuestionDetail> {
  await delay(100);
  for (const list of followUps.values()) {
    const found = list.find((f) => f.id === id);
    if (found) return found;
  }
  throw new ApiError(404, {
    status: 404,
    title: `Follow-up question with id ${id} was not found.`,
  });
}

export async function createFollowUp(
  req: CreateFollowUpQuestionRequest,
): Promise<FollowUpQuestionResult> {
  await delay(2800);
  const supportsUser = /语域|正式|register|可接受|也可以|标准/.test(req.questionText);
  const verdict = supportsUser ? FollowUpVerdict.user_correct : FollowUpVerdict.partial;
  const now = new Date().toISOString();
  const followUpId = nextId("fu");
  const aiResponse = supportsUser
    ? "复核后认为你的判断成立：该表达在澳洲政府公文语境中属可接受的正式用法，原判定对语域的扣分过严。相关评分标准已生成修正记录并进入生效流程。"
    : "你的理由部分成立：该处译法在口语场景确实常见，但本题为面向公众的正式通知，原判定的方向仍然保留；已将该判定的扣分权重下调，并在错误说明中补充语境条件。";
  const result: FollowUpQuestionResult = {
    id: followUpId,
    submissionId: req.submissionId,
    verdict,
    aiResponse,
    submissionStatus: supportsUser ? SubmissionStatus.standard_revised : SubmissionStatus.graded,
    standardOverrideId: supportsUser ? nextId("ovr") : null,
    standardOverrideStatus: supportsUser ? OverrideStatus.active : null,
  };
  const detail: FollowUpQuestionDetail = {
    id: followUpId,
    submissionId: req.submissionId,
    userId: req.userId,
    contextRef: req.contextRef,
    questionText: req.questionText,
    aiResponse,
    verdict,
    createdAt: now,
  };
  const list = followUps.get(req.submissionId) ?? [];
  followUps.set(req.submissionId, [...list, detail]);
  const s = submissions.get(req.submissionId);
  if (s) submissions.set(req.submissionId, { ...s, status: result.submissionStatus });
  if (result.standardOverrideId) {
    overrides.unshift({
      id: result.standardOverrideId,
      scope: OverrideScope.grading_rubric,
      dimensionOrRule: "textual_norms",
      originalRuleText: "该场景默认按扣分处理。",
      revisedRuleText: `由追问「${req.questionText.slice(0, 40)}」引发的评分标准修正。`,
      triggeredByFollowupId: followUpId,
      status: OverrideStatus.active,
      previousOverrideId: null,
      effectiveFrom: now,
      createdAt: now,
    });
  }
  return result;
}

export async function listStandardOverrides(status?: number): Promise<StandardOverride[]> {
  await delay(120);
  return status === undefined ? overrides : overrides.filter((o) => o.status === status);
}

export async function getStandardOverrideById(id: string): Promise<StandardOverrideDetail> {
  await delay(100);
  const found = overrides.find((o) => o.id === id);
  if (!found)
    throw new ApiError(404, {
      status: 404,
      title: `Standard override with id ${id} was not found.`,
    });
  return found;
}

/** 对应后端 POST /standard-overrides/{id}/activate——design doc §10.6"或经过一次人工复核"路径，
 * 不看累计确认次数直接把 observing 提升为 active。 */
export async function activateStandardOverride(
  id: string,
): Promise<ActivateStandardOverrideResult> {
  await delay(200);
  const found = overrides.find((o) => o.id === id);
  if (!found)
    throw new ApiError(404, {
      status: 404,
      title: `Standard override with id ${id} was not found.`,
    });
  if (found.status !== OverrideStatus.observing) {
    throw new ApiError(409, {
      status: 409,
      title: `Standard override ${id} is not in the observing state.`,
    });
  }
  found.status = OverrideStatus.active;
  found.effectiveFrom = found.effectiveFrom ?? new Date().toISOString();
  return { id: found.id, status: found.status, effectiveFrom: found.effectiveFrom };
}

/* ------------------------------ 深入学习 ------------------------------ */

export async function generateDeepLearning(
  questionId: string,
): Promise<GenerateDeepLearningContentResponse> {
  const cached = deepLearningCache.get(questionId);
  if (cached) {
    await delay(180);
    return { ...cached, wasCached: true };
  }
  await delay(3400);
  const q = questions.find((item) => item.id === questionId);
  const content: DeepLearningContent = {
    questionId,
    referenceText:
      q?.taskType === 1
        ? "尊敬的居民：市议会将自下周一起对中央大道进行为期四周的路面翻修。施工期间，12 路与 30 路公交将改道行驶，请提前规划出行。如有紧急就医需要，请致电市议会服务热线，我们将为您安排优先通行安排。感谢您的耐心与配合。"
        : "社区健康中心将自 5 月 6 日起，为 65 岁及以上居民免费提供流感疫苗接种。建议提前预约；每日上午另设少量现场名额。如您目前身体不适，或过去 48 小时内出现发热，请推迟预约并致电本中心。陪同符合条件居民前来的照护者，同样可免费接种。",
    comparisonNotes:
      "参考译文的三处关键处理：① 「aged 65 and over」译为「及以上」以保留含端点语义；② 英文被动与情态句改写为中文公告惯用的主动祈使；③ 冒号统领的通知开头替代英文称谓句，符合中文公文体例。",
    sentencePatterns: [
      {
        id: "sp-1",
        patternName: "will offer … to … from + 日期（公告类起始时间）",
        exampleSentence: "The Centre will offer free vaccinations to residents from 6 May.",
        breakdownSteps: "识别施动机构 → 提取受益对象 → 时间状语前置到中文句首「自……起」",
        variants: "will commence / will be available from / effective from",
        domain: "医疗健康",
        scenario: "公告通知",
        frequencyTag: "高频",
      },
      {
        id: "sp-2",
        patternName: "Residents who … should …（条件+建议）",
        exampleSentence: "Residents who have had a fever should postpone their appointment.",
        breakdownSteps: "定语从句转为中文前置条件小句「如您……，请……」",
        variants: "Those who … are advised to / If you …, please …",
        domain: "医疗健康",
        scenario: "公告通知",
        frequencyTag: "高频",
      },
    ],
    vocabExpressions: [
      {
        id: "ve-1",
        englishExpr: "walk-in slots",
        chineseEquiv: "现场名额 / 免预约名额",
        contextNote: "医疗与政务服务场景常见，避免直译为「走进名额」。",
        category: "服务流程",
        domain: "医疗健康",
        scenario: "公告通知",
        frequencyTag: "中频",
        literalTranslatable: false,
      },
      {
        id: "ve-2",
        englishExpr: "carers accompanying …",
        chineseEquiv: "陪同……的照护者",
        contextNote: "carer 在澳洲语境指领取照护津贴的照料者，不宜译为「护工」。",
        category: "身份称谓",
        domain: "医疗健康",
        scenario: "信函往来",
        frequencyTag: "中频",
        literalTranslatable: true,
      },
      {
        id: "ve-3",
        englishExpr: "at no cost",
        chineseEquiv: "免费 / 无需付费",
        contextNote: "比 free 更正式，公告文本中优先使用。",
        category: "费用表述",
        domain: "商务财经",
        scenario: "公告通知",
        frequencyTag: "高频",
        literalTranslatable: true,
      },
    ],
  };
  deepLearningCache.set(questionId, content);
  return { ...content, wasCached: false };
}

/* --------------------------- 薄弱点 / 进度 / 复习库 --------------------------- */

export async function listWeakPoints(status?: number): Promise<WeakPoint[]> {
  await delay(140);
  const all: WeakPoint[] = [
    {
      id: "wp-1",
      label: "语域一致性",
      patternSummary: "正式通知中混入口语表达（「大家」「记得」等），近一个月出现 7 次。",
      firstDetectedAt: "2026-05-12T00:00:00Z",
      lastSeenAt: "2026-08-27T00:00:00Z",
      recurrenceCount: 7,
      status: WeakPointStatus.active,
      priority: Priority.high,
    },
    {
      id: "wp-2",
      label: "长句拆分",
      patternSummary: "英文多重定语从句直译为中文长句，读者阅读负担偏重。",
      firstDetectedAt: "2026-04-03T00:00:00Z",
      lastSeenAt: "2026-08-19T00:00:00Z",
      recurrenceCount: 5,
      status: WeakPointStatus.active,
      priority: Priority.high,
    },
    {
      id: "wp-3",
      label: "数量与端点表述",
      patternSummary: "“and over / up to / within” 等端点是否含本数处理不稳定。",
      firstDetectedAt: "2026-06-01T00:00:00Z",
      lastSeenAt: "2026-08-05T00:00:00Z",
      recurrenceCount: 3,
      status: WeakPointStatus.active,
      priority: Priority.medium,
    },
    {
      id: "wp-4",
      label: "机构名称术语",
      patternSummary: "City Council / Tribunal 等机构名译法此前不统一，近两月未再出现。",
      firstDetectedAt: "2026-02-18T00:00:00Z",
      lastSeenAt: "2026-06-22T00:00:00Z",
      recurrenceCount: 4,
      status: WeakPointStatus.resolved,
      priority: Priority.low,
    },
  ];
  return status === undefined ? all : all.filter((w) => w.status === status);
}

export async function listProgress(difficultyTier?: string): Promise<ProgressSnapshot[]> {
  await delay(160);
  const rows: ProgressSnapshot[] = [
    {
      id: "ps-1",
      periodStart: "2026-03-01",
      periodEnd: "2026-03-31",
      difficultyTier: "medium",
      avgBandMeaningTransfer: 3.4,
      avgBandTextualNorms: 3.8,
      avgBandLanguageProficiency: 3.2,
      passRate: 0.28,
      trendNote: "起步阶段，语篇规范是主要失分项。",
      keyTurningPoint: false,
    },
    {
      id: "ps-2",
      periodStart: "2026-04-01",
      periodEnd: "2026-04-30",
      difficultyTier: "medium",
      avgBandMeaningTransfer: 3.1,
      avgBandTextualNorms: 3.6,
      avgBandLanguageProficiency: 3.0,
      passRate: 0.36,
      trendNote: "意义传递稳步改善。",
      keyTurningPoint: false,
    },
    {
      id: "ps-3",
      periodStart: "2026-05-01",
      periodEnd: "2026-05-31",
      difficultyTier: "medium",
      avgBandMeaningTransfer: 2.7,
      avgBandTextualNorms: 3.4,
      avgBandLanguageProficiency: 2.8,
      passRate: 0.47,
      trendNote: "开始系统练习公告体例后，通过率首次接近半数。",
      keyTurningPoint: true,
    },
    {
      id: "ps-4",
      periodStart: "2026-06-01",
      periodEnd: "2026-06-30",
      difficultyTier: "medium",
      avgBandMeaningTransfer: 2.5,
      avgBandTextualNorms: 3.1,
      avgBandLanguageProficiency: 2.6,
      passRate: 0.55,
      trendNote: "三个维度同步提升。",
      keyTurningPoint: false,
    },
    {
      id: "ps-5",
      periodStart: "2026-07-01",
      periodEnd: "2026-07-31",
      difficultyTier: "medium",
      avgBandMeaningTransfer: 2.3,
      avgBandTextualNorms: 2.9,
      avgBandLanguageProficiency: 2.4,
      passRate: 0.63,
      trendNote: "语域问题仍是唯一未达标维度。",
      keyTurningPoint: false,
    },
    {
      id: "ps-6",
      periodStart: "2026-08-01",
      periodEnd: "2026-08-31",
      difficultyTier: "medium",
      avgBandMeaningTransfer: 2.1,
      avgBandTextualNorms: 2.6,
      avgBandLanguageProficiency: 2.3,
      passRate: 0.71,
      trendNote: "语篇规范首次逼近合格线，建议下阶段挑战 hard 难度真题。",
      keyTurningPoint: true,
    },
  ];
  return difficultyTier ? rows.filter((r) => r.difficultyTier === difficultyTier) : rows;
}

const reviewPatterns: ReviewPatternItem[] = [
  {
    id: "rp-1",
    questionId: "q-1001",
    patternName: "will offer … from + 日期",
    exampleSentence: "The Centre will offer free vaccinations from 6 May.",
    domain: "医疗健康",
    scenario: "公告通知",
    frequencyTag: "高频",
    timesEncountered: 6,
    masteryLevel: MasteryLevel.Familiar,
    lastReviewedAt: "2026-08-21T00:00:00Z",
  },
  {
    id: "rp-2",
    questionId: "q-1002",
    patternName: "Should X default …, Y may …（法律条件句）",
    exampleSentence: "Should the tenant default, the landlord may reinstate the application.",
    domain: "法律政务",
    scenario: "信函往来",
    frequencyTag: "中频",
    timesEncountered: 3,
    masteryLevel: MasteryLevel.New,
    lastReviewedAt: null,
  },
  {
    id: "rp-3",
    questionId: "q-1003",
    patternName: "If you require …, please …",
    exampleSentence: "If you require an interpreter, please tick the box.",
    domain: "法律政务",
    scenario: "公告通知",
    frequencyTag: "高频",
    timesEncountered: 9,
    masteryLevel: MasteryLevel.Mastered,
    lastReviewedAt: "2026-08-28T00:00:00Z",
  },
  {
    id: "rp-4",
    questionId: "q-1005",
    patternName: "You will not be held liable provided …",
    exampleSentence: "You will not be held liable provided you notify us within thirty days.",
    domain: "商务财经",
    scenario: "信函往来",
    frequencyTag: "中频",
    timesEncountered: 2,
    masteryLevel: MasteryLevel.New,
    lastReviewedAt: null,
  },
];

const reviewVocab: ReviewVocabItem[] = [
  {
    id: "rv-1",
    questionId: "q-1001",
    englishExpr: "walk-in slots",
    chineseEquiv: "现场名额",
    domain: "医疗健康",
    scenario: "公告通知",
    frequencyTag: "中频",
    timesEncountered: 4,
    masteryLevel: MasteryLevel.Familiar,
    lastReviewedAt: "2026-08-12T00:00:00Z",
  },
  {
    id: "rv-2",
    questionId: "q-1002",
    englishExpr: "conciliation conference",
    chineseEquiv: "调解会议",
    domain: "法律政务",
    scenario: "信函往来",
    frequencyTag: "低频",
    timesEncountered: 1,
    masteryLevel: MasteryLevel.New,
    lastReviewedAt: null,
  },
  {
    id: "rv-3",
    questionId: "q-1002",
    englishExpr: "in six equal monthly instalments",
    chineseEquiv: "分六期等额按月偿付",
    domain: "法律政务",
    scenario: "信函往来",
    frequencyTag: "中频",
    timesEncountered: 3,
    masteryLevel: MasteryLevel.Familiar,
    lastReviewedAt: "2026-07-30T00:00:00Z",
  },
  {
    id: "rv-4",
    questionId: "q-1005",
    englishExpr: "unauthorised transactions",
    chineseEquiv: "未经授权的交易",
    domain: "商务财经",
    scenario: "信函往来",
    frequencyTag: "高频",
    timesEncountered: 7,
    masteryLevel: MasteryLevel.Mastered,
    lastReviewedAt: "2026-08-26T00:00:00Z",
  },
  {
    id: "rv-5",
    questionId: "q-1004",
    englishExpr: "road resurfacing works",
    chineseEquiv: "路面翻修工程",
    domain: "法律政务",
    scenario: "公告通知",
    frequencyTag: "中频",
    timesEncountered: 2,
    masteryLevel: MasteryLevel.New,
    lastReviewedAt: null,
  },
];

export async function listReviewPatterns() {
  await delay(140);
  return [...reviewPatterns];
}
export async function listReviewVocab() {
  await delay(140);
  return [...reviewVocab];
}
export async function reviewPattern(id: string, masteryLevel: number) {
  await delay(200);
  const item = reviewPatterns.find((p) => p.id === id);
  if (item) {
    item.masteryLevel = masteryLevel;
    item.lastReviewedAt = new Date().toISOString();
    item.timesEncountered += 1;
  }
  return item;
}
export async function reviewVocabItem(id: string, masteryLevel: number) {
  await delay(200);
  const item = reviewVocab.find((v) => v.id === id);
  if (item) {
    item.masteryLevel = masteryLevel;
    item.lastReviewedAt = new Date().toISOString();
    item.timesEncountered += 1;
  }
  return item;
}

/* ------------------------------- 错误类型 ------------------------------- */

export class ApiError extends Error {
  constructor(
    public status: number,
    public problem: ProblemDetails | null,
  ) {
    super(problem?.title ?? `Request failed with status ${status}`);
    this.name = "ApiError";
  }
}
