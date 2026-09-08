"use client";

import Link from "next/link";
import { useParams, useRouter } from "next/navigation";
import { useState } from "react";
import { useMutation, useQuery } from "@tanstack/react-query";
import { Highlighter, Send, Trash2 } from "lucide-react";
import { PageShell } from "@/components/shell/page-shell";
import { ArticleText } from "@/components/shared/article-text";
import { ErrorBanner } from "@/components/shared/ai-loading-state";
import { SelectableSourceText } from "@/components/practice/selectable-source-text";
import { DifficultyBadge, TaskTypeBadge } from "@/components/practice/difficulty-badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Textarea } from "@/components/ui/textarea";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Skeleton } from "@/components/ui/skeleton";
import { Badge } from "@/components/ui/badge";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { getQuestionById, listSeedReferences } from "@/lib/api/questions";
import { createSubmission } from "@/lib/api/submissions";
import { useErrorTaxonomies, useExamType } from "@/hooks/use-exam-config";
import { useCurrentUser } from "@/hooks/use-current-user";
import { QuestionOrigin, TaskType } from "@/lib/types/enums";
import type { TaskBAnnotation } from "@/lib/types/dtos";
import { taskAContentSchema, taskBContentSchema } from "@/lib/validation/submission";
import { useT } from "@/lib/i18n";

// 展示顺序：grid 逐行填充，两列 —— 第一行 Domain / Topic、Purpose，第二行 Text type、Audience。
const BRIEF_ORDER = [
  "domain",
  "topic",
  "领域",
  "purpose",
  "目的",
  "textType",
  "文本类型",
  "audience",
  "受众",
];

/**
 * brief 落在后端 jsonb 列（设计文档 §6.2：领域/文本类型/目的/受众），存的是一段 JSON 字符串。
 * 答题页不再把原始 JSON 丢给用户，而是拆成「label: value」两条一行展示。
 * 键可能是英文（domain/textType/purpose/audience）也可能是中文（领域/文本类型/目的/受众）；
 * labels 由调用方按当前界面语言传入。
 */
function parseBrief(
  brief: string | null,
  labels: Record<string, string>,
): { label: string; value: string }[] | null {
  if (!brief) return null;
  let parsed: unknown;
  try {
    parsed = JSON.parse(brief);
  } catch {
    return null;
  }
  if (!parsed || typeof parsed !== "object" || Array.isArray(parsed)) return null;
  const rank = (k: string) => {
    const i = BRIEF_ORDER.indexOf(k);
    return i === -1 ? BRIEF_ORDER.length : i;
  };
  const entries = Object.entries(parsed as Record<string, unknown>)
    .filter(([, v]) => v != null && String(v).trim() !== "")
    .sort(([a], [b]) => rank(a) - rank(b))
    .map(([k, v]) => ({ label: labels[k] ?? k, value: String(v) }));
  return entries.length ? entries : null;
}

export function AnswerPage() {
  const t = useT();
  const { questionId } = useParams<{ questionId: string }>();
  const router = useRouter();
  const examType = useExamType();
  const currentUser = useCurrentUser();
  const errorTaxonomies = useErrorTaxonomies(examType.data?.id);
  const question = useQuery({
    queryKey: ["question", questionId],
    queryFn: () => getQuestionById(questionId),
  });
  // design doc §11.2 Step 8 的真题溯源：AI 出题时参考了哪些真题种子，只对 ai_generated 题目有意义。
  const seedReferences = useQuery({
    queryKey: ["seed-references", questionId],
    queryFn: () => listSeedReferences(questionId),
    enabled: question.data?.origin === QuestionOrigin.ai_generated,
  });

  const [translation, setTranslation] = useState("");
  const [annotations, setAnnotations] = useState<TaskBAnnotation[]>([]);
  const [draft, setDraft] = useState<{ start: number; end: number } | null>(null);
  // 以前是 useState(errorTaxonomies[0]!.categoryKey) 同步初始化——现在 errorTaxonomies 要异步查询
  // 才能拿到，所以初始为空字符串，渲染 Select 时 fallback 到已加载数据的第一项。
  const [draftCategory, setDraftCategory] = useState("");
  const [draftCorrected, setDraftCorrected] = useState("");
  const selectedDraftCategory = draftCategory || errorTaxonomies.data?.[0]?.categoryKey || "";

  const submit = useMutation({
    mutationFn: () => {
      const isTaskB = question.data?.taskType === TaskType.B;
      return createSubmission({
        questionId,
        userId: currentUser.data!.id,
        taskType: question.data?.taskType ?? TaskType.A,
        content: JSON.stringify(isTaskB ? annotations : translation),
      });
    },
    onSuccess: (submission) => router.push(`/submissions/${submission.id}`),
  });

  const briefLabels: Record<string, string> = {
    domain: t("answer.brief.domain"),
    topic: t("answer.brief.domain"),
    领域: t("answer.brief.domain"),
    textType: t("answer.brief.textType"),
    文本类型: t("answer.brief.textType"),
    purpose: t("answer.brief.purpose"),
    目的: t("answer.brief.purpose"),
    audience: t("answer.brief.audience"),
    受众: t("answer.brief.audience"),
  };

  if (question.isPending) {
    return (
      <PageShell title={t("answer.title")}>
        <Skeleton className="h-96 w-full rounded-xl" />
      </PageShell>
    );
  }

  if (question.isError || !question.data) {
    return (
      <PageShell title={t("answer.title")}>
        <ErrorBanner error={question.error} />
      </PageShell>
    );
  }

  const q = question.data;
  const isTaskB = q.taskType === TaskType.B;
  const flawed = q.taskB?.flawedTranslationText ?? "";
  const briefEntries = parseBrief(q.brief, briefLabels);
  const wordCount = q.wordCount ?? q.sourceText.trim().split(/\s+/).filter(Boolean).length;
  // 提交前的前端校验镜像后端 CreateSubmissionValidator（方案 §11），提前拦截而非等后端 400。
  const contentValidation = isTaskB
    ? taskBContentSchema.safeParse(annotations)
    : taskAContentSchema.safeParse(translation);
  const canSubmit = contentValidation.success;

  // PageHeader 把 description 包在 <p> 里，只能放 phrasing content，所以这里一律用 <span>
  // （<dl>/<p> 嵌进 <p> 会被浏览器拆出去，触发 Next.js hydration 不一致）。
  const briefNode = briefEntries ? (
    <span className="grid max-w-3xl gap-x-6 gap-y-1 font-sans text-xs font-normal text-muted-foreground sm:grid-cols-[max-content_max-content]">
      {briefEntries.map((e) => (
        <span key={e.label} className="flex gap-1.5">
          <span className="shrink-0 font-medium">{e.label}:</span>
          <span className="min-w-0">{e.value}</span>
        </span>
      ))}
    </span>
  ) : q.brief ? (
    <span className="block max-w-3xl font-sans text-xs font-normal text-muted-foreground">
      {q.brief}
    </span>
  ) : null;

  return (
    <PageShell
      title={q.title}
      description={briefNode}
      actions={
        <>
          <TaskTypeBadge taskType={q.taskType} />
          <DifficultyBadge difficulty={q.difficulty} />
          <Badge variant="outline" className="text-numeric border-border text-muted-foreground">
            {t("questionCard.words", { count: wordCount })}
          </Badge>
        </>
      }
    >
      <div className="grid gap-6 lg:h-full lg:min-h-0 lg:grid-cols-2">
        <div className="flex min-h-0 flex-col gap-6 lg:overflow-hidden">
          <Card className="flex min-h-0 flex-1 flex-col border-border shadow-none">
            <CardHeader className="shrink-0">
              <CardTitle className="text-base">{t("answer.sourceText")}</CardTitle>
            </CardHeader>
            <CardContent className="min-h-0 flex-1 space-y-4 overflow-y-auto">
              {q.title?.trim() ? (
                <h2 className="text-[15px] font-semibold leading-snug text-foreground">
                  {q.title}
                </h2>
              ) : null}
              <ArticleText text={q.sourceText} className="text-[15px]" />
            </CardContent>
          </Card>

          {seedReferences.data?.length ? (
            <Card className="border-border shadow-none lg:max-h-[38%] lg:shrink-0 lg:overflow-y-auto">
              <CardHeader>
                <CardTitle className="text-base">{t("answer.referenceQuestions")}</CardTitle>
              </CardHeader>
              <CardContent className="space-y-2">
                {seedReferences.data.map((r) => (
                  <div key={r.id} className="rounded-md border border-border p-3 text-sm">
                    <Link
                      href={`/practice/${r.seedQuestionId}`}
                      className="text-primary underline underline-offset-2"
                    >
                      {r.seedQuestionTitle}
                    </Link>
                    {r.similarityReason ? (
                      <p className="mt-1 text-xs text-muted-foreground">{r.similarityReason}</p>
                    ) : null}
                  </div>
                ))}
              </CardContent>
            </Card>
          ) : null}
        </div>

        <div className="flex min-h-0 flex-col gap-6 lg:overflow-hidden">
          {isTaskB ? (
            <Card className="flex min-h-0 flex-1 flex-col border-border shadow-none">
              <CardHeader className="shrink-0">
                <CardTitle className="flex items-center gap-2 text-base">
                  <Highlighter className="size-4 text-accent" />
                  {t("answer.flawedTranslation")}
                </CardTitle>
              </CardHeader>
              <CardContent className="min-h-0 flex-1 space-y-4 overflow-y-auto">
                <SelectableSourceText
                  text={flawed}
                  highlightRanges={annotations.map((a) => ({
                    positionStart: a.positionStart,
                    positionEnd: a.positionEnd,
                    tone: "flag" as const,
                  }))}
                  onSelectRange={(start, end) => {
                    setDraft({ start, end });
                    setDraftCorrected(flawed.slice(start, end));
                  }}
                />

                {draft ? (
                  <div className="space-y-3 rounded-lg border border-primary/40 bg-primary/5 p-4">
                    <p className="text-numeric text-xs text-muted-foreground">
                      {t("answer.selection", {
                        start: draft.start,
                        end: draft.end,
                        text: flawed.slice(draft.start, draft.end),
                      })}
                    </p>
                    <div className="space-y-2">
                      <Label>{t("answer.errorType")}</Label>
                      <Select value={selectedDraftCategory} onValueChange={setDraftCategory}>
                        <SelectTrigger>
                          <SelectValue />
                        </SelectTrigger>
                        <SelectContent>
                          {(errorTaxonomies.data ?? []).map((tax) => (
                            <SelectItem key={tax.id} value={tax.categoryKey}>
                              {tax.categoryName}
                            </SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                    </div>
                    <div className="space-y-2">
                      <Label>{t("answer.correctedText")}</Label>
                      <Input
                        value={draftCorrected}
                        onChange={(e) => setDraftCorrected(e.target.value)}
                      />
                    </div>
                    <div className="flex gap-2">
                      <Button
                        size="sm"
                        disabled={!selectedDraftCategory}
                        onClick={() => {
                          setAnnotations((prev) => [
                            ...prev,
                            {
                              positionStart: draft.start,
                              positionEnd: draft.end,
                              errorCategory: selectedDraftCategory,
                              correctedText: draftCorrected,
                            },
                          ]);
                          setDraft(null);
                        }}
                      >
                        {t("answer.addAnnotation")}
                      </Button>
                      <Button size="sm" variant="ghost" onClick={() => setDraft(null)}>
                        {t("common.cancel")}
                      </Button>
                    </div>
                  </div>
                ) : null}

                <div className="space-y-2">
                  <p className="text-numeric text-xs font-medium text-muted-foreground">
                    {t("answer.annotatedCount", { count: annotations.length })}
                  </p>
                  {annotations.map((a, i) => (
                    <div
                      key={`${a.positionStart}-${a.positionEnd}-${i}`}
                      className="flex items-start justify-between gap-3 rounded-md border border-border p-3"
                    >
                      <div className="space-y-1">
                        <div className="flex items-center gap-2">
                          <Badge variant="outline" className="border-accent/40 text-accent">
                            {errorTaxonomies.data?.find(
                              (tax) => tax.categoryKey === a.errorCategory,
                            )?.categoryName ?? a.errorCategory}
                          </Badge>
                          <span className="text-numeric text-xs text-muted-foreground">
                            [{a.positionStart}, {a.positionEnd})
                          </span>
                        </div>
                        <p className="text-sm">
                          <span className="line-through opacity-60">
                            {flawed.slice(a.positionStart, a.positionEnd)}
                          </span>
                          <span className="mx-1">→</span>
                          <span className="text-primary">{a.correctedText}</span>
                        </p>
                      </div>
                      <Button
                        size="icon"
                        variant="ghost"
                        onClick={() => setAnnotations((prev) => prev.filter((_, idx) => idx !== i))}
                      >
                        <Trash2 className="size-4" />
                      </Button>
                    </div>
                  ))}
                </div>
              </CardContent>
            </Card>
          ) : (
            <Card className="flex min-h-0 flex-1 flex-col border-border shadow-none">
              <CardHeader className="shrink-0">
                <CardTitle className="text-base">{t("answer.yourTranslation")}</CardTitle>
              </CardHeader>
              <CardContent className="flex min-h-0 flex-1 flex-col gap-3">
                <Textarea
                  rows={16}
                  value={translation}
                  onChange={(e) => setTranslation(e.target.value)}
                  placeholder={t("answer.translationPlaceholder")}
                  className="source-text resize-none lg:min-h-0 lg:flex-1"
                />
                <p className="text-numeric shrink-0 text-xs text-muted-foreground">
                  {t("answer.charCount", { count: translation.length })}
                </p>
              </CardContent>
            </Card>
          )}

          <div className="shrink-0 space-y-3">
            <Button
              className="w-full"
              disabled={!canSubmit || submit.isPending || !currentUser.data}
              onClick={() => submit.mutate()}
            >
              <Send className="size-4" />
              {submit.isPending ? t("answer.submitting") : t("answer.submit")}
            </Button>
            {submit.isError ? <ErrorBanner error={submit.error} /> : null}
          </div>
        </div>
      </div>
    </PageShell>
  );
}
