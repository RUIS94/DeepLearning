"use client";

import { useMemo } from "react";
import { useParams } from "next/navigation";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { BookOpenCheck, Loader2, Quote } from "lucide-react";
import { AppShell } from "@/components/shared/app-shell";
import { ArticleText } from "@/components/shared/article-text";
import { ErrorBanner } from "@/components/shared/ai-loading-state";
import { FollowUpPanel } from "@/components/grading/follow-up-panel";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { generateDeepLearning } from "@/lib/api/deep-learning";
import { getQuestionById } from "@/lib/api/questions";
import { listSubmissions } from "@/lib/api/submissions";
import { useExamType } from "@/hooks/use-exam-config";
import { useCurrentUser } from "@/hooks/use-current-user";
import { useT } from "@/lib/i18n";
import { cn } from "@/lib/utils";
import type { SentencePattern, VocabExpression } from "@/lib/types/dtos";

/** breakdownSteps 后端存的是 AI 返回的原始 JSON（对象 {"主干": "...", ...} 或普通字符串）——两种都兜住。 */
function BreakdownSteps({ raw }: { raw: string }) {
  let parsed: unknown;
  try {
    parsed = JSON.parse(raw);
  } catch {
    parsed = null;
  }
  if (parsed && typeof parsed === "object" && !Array.isArray(parsed)) {
    const entries = Object.entries(parsed as Record<string, unknown>).filter(
      ([, v]) => v != null && String(v).trim() !== "",
    );
    if (entries.length > 0) {
      return (
        <dl className="mt-2 space-y-1.5 text-sm leading-relaxed">
          {entries.map(([k, v]) => (
            <div key={k} className="flex gap-2">
              <dt className="shrink-0 font-medium text-muted-foreground">{k}</dt>
              <dd>{String(v)}</dd>
            </div>
          ))}
        </dl>
      );
    }
  }
  return <p className="mt-2 text-sm leading-relaxed">{raw}</p>;
}

/** comparisonNotes 后端存的是 AI 返回的原始 JSON（多为字符串数组）——数组渲染成列表，纯字符串照原样。 */
function ComparisonNotes({ raw }: { raw: string }) {
  let parsed: unknown;
  try {
    parsed = JSON.parse(raw);
  } catch {
    parsed = null;
  }
  const items = Array.isArray(parsed) ? parsed.map((x) => String(x).trim()).filter(Boolean) : null;
  if (items && items.length > 0) {
    return (
      <ul className="list-disc space-y-1 pl-4">
        {items.map((note, i) => (
          <li key={i}>{note}</li>
        ))}
      </ul>
    );
  }
  return <>{raw}</>;
}

/** 标签行（不着色，muted 描边）——Vocabulary 区域用，默认放在单词上方。 */
function TagRow({
  tags,
  className = "mb-2",
}: {
  tags: (string | null | undefined)[];
  className?: string;
}) {
  const shown = tags.filter((t): t is string => !!t);
  if (shown.length === 0) return null;
  return (
    <div className={cn("flex flex-wrap gap-2", className)}>
      {shown.map((tag) => (
        <Badge key={tag} variant="outline" className="border-border text-muted-foreground">
          {tag}
        </Badge>
      ))}
    </div>
  );
}

/** 频次标签配色：高频=primary、中频=warning、低频=muted。 */
function freqTone(tag: string): string {
  if (/高|high/i.test(tag)) return "border-primary/40 text-primary";
  if (/中|mid|medium/i.test(tag)) return "border-warning/40 text-warning-foreground";
  return "border-border text-muted-foreground";
}

/** domain / scenario / frequency 三个标签，显示在 item 右上角；频次标签按高/中/低着色。 */
function ItemTags({
  domain,
  scenario,
  freq,
}: {
  domain?: string | null;
  scenario?: string | null;
  freq?: string | null;
}) {
  const tags: { text: string; tone: string }[] = [];
  if (domain) tags.push({ text: domain, tone: "border-border text-muted-foreground" });
  if (scenario) tags.push({ text: scenario, tone: "border-border text-muted-foreground" });
  if (freq) tags.push({ text: freq, tone: freqTone(freq) });
  if (tags.length === 0) return null;
  return (
    <div className="flex shrink-0 flex-wrap justify-end gap-1.5">
      {tags.map((tg, i) => (
        <Badge key={i} variant="outline" className={cn("text-xs", tg.tone)}>
          {tg.text}
        </Badge>
      ))}
    </div>
  );
}

function SentencePatternCard({ p }: { p: SentencePattern }) {
  const t = useT();
  return (
    <div className="border-b border-dashed border-border py-4 first:pt-0 last:border-0 last:pb-0">
      <div className="flex items-start justify-between gap-3">
        <p className="text-sm font-medium">{p.patternName}</p>
        <ItemTags domain={p.domain} scenario={p.scenario} freq={p.frequencyTag} />
      </div>
      {p.exampleSentence ? (
        <p className="mt-2 flex gap-2 text-sm text-muted-foreground">
          <Quote className="mt-0.5 size-3.5 shrink-0" />
          {p.exampleSentence}
        </p>
      ) : null}
      {p.breakdownSteps ? <BreakdownSteps raw={p.breakdownSteps} /> : null}
      {p.variants ? (
        <p className="mt-2 text-xs leading-relaxed text-muted-foreground">
          <span className="font-medium">{t("deepLearning.commonVariants")}</span>
          {p.variants}
        </p>
      ) : null}
    </div>
  );
}

function VocabCard({ v }: { v: VocabExpression }) {
  const t = useT();
  return (
    <div className="border-b border-dashed border-border py-4 last:border-0 last:pb-0">
      <TagRow tags={[v.domain, v.scenario, v.frequencyTag]} />
      <p className="flex flex-wrap items-center gap-2 text-sm font-medium">
        {v.englishExpr}
        {v.literalTranslatable === false ? (
          <span className="rounded bg-warning/15 px-1.5 py-0.5 text-[10px] font-medium text-warning-foreground">
            {t("deepLearning.notLiteral")}
          </span>
        ) : null}
      </p>
      {v.chineseEquiv ? <p className="mt-1 text-sm text-primary">{v.chineseEquiv}</p> : null}
      {v.contextNote ? (
        <p className="mt-2 text-xs leading-relaxed text-muted-foreground">{v.contextNote}</p>
      ) : null}
    </div>
  );
}

const UNCATEGORIZED = "其他";

export function DeepLearningPage() {
  const t = useT();
  const { questionId } = useParams<{ questionId: string }>();
  const examType = useExamType();
  const currentUser = useCurrentUser();
  const queryClient = useQueryClient();

  const question = useQuery({
    queryKey: ["question", questionId],
    queryFn: () => getQuestionById(questionId),
  });
  const content = useQuery({
    queryKey: ["deep-learning", questionId],
    queryFn: () => generateDeepLearning(questionId, examType.data!.id),
    enabled: !!examType.data,
    // 这个 queryFn 会触发一次真实的 AI 生成（后端自身已重试 3 次），失败后前端不该再
    // 自动重试 3 次——那会变成 4×3=12 次昂贵调用。成功即长期有效（后端按题缓存）。
    retry: 1,
    retryDelay: 3000,
    staleTime: Infinity,
    gcTime: Infinity,
    refetchOnWindowFocus: false,
    refetchOnReconnect: false,
  });

  // 追问 popup 与「当前提交」绑定——即本题最近一次提交。上一步（批改页）发起的追问用的是
  // 同一个 submissionId，所以在这里能原样看到、继续追问。
  const submissions = useQuery({
    queryKey: ["submissions", currentUser.data?.id, questionId],
    queryFn: () => listSubmissions(currentUser.data!.id, questionId),
    enabled: !!currentUser.data,
  });
  const submissionId = submissions.data?.[0]?.id ?? null;

  // 词汇与表达按 category 分组呈现，让「专业术语 / 固定搭配 / 俚语 / 词组…」的覆盖面一目了然。
  const vocabGroups = useMemo(() => {
    const groups = new Map<string, VocabExpression[]>();
    for (const v of content.data?.vocabExpressions ?? []) {
      const key = v.category?.trim() || UNCATEGORIZED;
      const bucket = groups.get(key);
      if (bucket) bucket.push(v);
      else groups.set(key, [v]);
    }
    // 插入顺序保留，只把「其他」挪到最后
    return [...groups.entries()].sort(([a], [b]) =>
      a === UNCATEGORIZED ? 1 : b === UNCATEGORIZED ? -1 : 0,
    );
  }, [content.data?.vocabExpressions]);

  return (
    <AppShell
      title={t("deepLearning.title")}
      description={question.data?.title}
      back
      backHref={submissionId ? `/submissions/${submissionId}` : undefined}
      actions={
        <>
          {content.data ? (
            <Badge variant="outline" className="border-primary/30 text-primary">
              {content.data.wasCached
                ? t("deepLearning.cached")
                : t("deepLearning.freshlyGenerated")}
            </Badge>
          ) : null}
          {submissionId ? (
            <FollowUpPanel
              submissionId={submissionId}
              onChanged={() =>
                queryClient.invalidateQueries({ queryKey: ["submission", submissionId] })
              }
            />
          ) : null}
        </>
      }
    >
      {content.isPending ? (
        // 和批改页的 loading 一致：整卡居中的大 spinner + 一句提示，页面稳定停在这里。
        <div className="flex min-h-full flex-col">
          <Card className="flex min-h-0 flex-1 flex-col border-border shadow-none">
            <CardHeader className="shrink-0">
              <CardTitle className="text-base">{t("deepLearning.generatingTitle")}</CardTitle>
            </CardHeader>
            <CardContent className="min-h-0 flex-1 overflow-y-auto">
              <div className="flex min-h-full flex-col items-center justify-center gap-3 py-10 text-center">
                <Loader2 className="size-8 animate-spin text-primary" />
                <p className="text-sm font-medium">{t("deepLearning.generating")}</p>
                <p className="max-w-md text-sm text-muted-foreground">
                  {t("deepLearning.pendingHint")}
                </p>
              </div>
            </CardContent>
          </Card>
        </div>
      ) : content.isError ? (
        <ErrorBanner error={content.error} />
      ) : content.data ? (
        // 高度链见 AGENTS.md「Full-height page layout」：grid lg:h-full 让左右两列等高、锁进视口；
        // 每列 lg:overflow-hidden，滚动交给列内的 overflow-y-auto 区域，页面本身不出滚动条。
        <div className="grid gap-6 lg:h-full lg:min-h-0 lg:grid-cols-[1fr_380px]">
          <div className="flex min-h-0 flex-col gap-6 lg:overflow-hidden">
            <div className="min-h-0 flex-1 lg:overflow-y-auto">
              <div className="flex flex-col gap-6">
                <Card className="border-border shadow-none">
                  <CardHeader>
                    <CardTitle className="flex items-center gap-2 text-base">
                      <BookOpenCheck className="size-4 text-primary" />
                      {t("deepLearning.referenceTranslation")}
                    </CardTitle>
                  </CardHeader>
                  <CardContent className="space-y-4">
                    {content.data.referenceTitle ? (
                      <p className="text-[15px] font-semibold leading-snug">
                        {content.data.referenceTitle}
                      </p>
                    ) : null}
                    <ArticleText text={content.data.referenceText} className="text-[15px]" />
                    {content.data.comparisonNotes ? (
                      <div className="rounded-lg bg-muted p-4 text-sm leading-relaxed">
                        <p className="mb-1 font-medium">{t("deepLearning.comparisonPoints")}</p>
                        <ComparisonNotes raw={content.data.comparisonNotes} />
                      </div>
                    ) : null}
                  </CardContent>
                </Card>

                <Card className="border-border shadow-none">
                  <CardHeader>
                    <CardTitle className="text-base">
                      {t("deepLearning.sentenceBreakdown")}
                    </CardTitle>
                  </CardHeader>
                  <CardContent className="pt-0">
                    {content.data.sentencePatterns.length === 0 ? (
                      <p className="text-sm text-muted-foreground">
                        {t("deepLearning.noSentencePatterns")}
                      </p>
                    ) : (
                      content.data.sentencePatterns.map((p) => (
                        <SentencePatternCard key={p.id} p={p} />
                      ))
                    )}
                  </CardContent>
                </Card>
              </div>
            </div>
          </div>

          <div className="flex min-h-0 flex-col gap-6 lg:overflow-hidden">
            <Card className="flex min-h-0 flex-1 flex-col border-border shadow-none">
              <CardHeader className="shrink-0">
                <CardTitle className="text-base">{t("deepLearning.vocabAndExpressions")}</CardTitle>
              </CardHeader>
              <CardContent className="min-h-0 flex-1 space-y-5 overflow-y-auto">
                {vocabGroups.length === 0 ? (
                  <p className="text-sm text-muted-foreground">{t("deepLearning.noVocab")}</p>
                ) : (
                  vocabGroups.map(([group, items]) => (
                    <div key={group}>
                      <p className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
                        {group === UNCATEGORIZED ? t("deepLearning.uncategorized") : group}
                      </p>
                      {items.map((v) => (
                        <VocabCard key={v.id} v={v} />
                      ))}
                    </div>
                  ))
                )}
              </CardContent>
            </Card>
          </div>
        </div>
      ) : null}
    </AppShell>
  );
}
