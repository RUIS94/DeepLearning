"use client";

import { useMemo } from "react";
import { useParams } from "next/navigation";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { BookOpenCheck, Quote } from "lucide-react";
import { AppShell } from "@/components/shared/app-shell";
import { ArticleText } from "@/components/shared/article-text";
import { AiLoadingState, ErrorBanner } from "@/components/shared/ai-loading-state";
import { FollowUpPanel } from "@/components/grading/follow-up-panel";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { generateDeepLearning } from "@/lib/api/deep-learning";
import { getQuestionById } from "@/lib/api/questions";
import { listSubmissions } from "@/lib/api/submissions";
import { useExamType } from "@/hooks/use-exam-config";
import { useCurrentUser } from "@/hooks/use-current-user";
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

function TagRow({ tags }: { tags: (string | null | undefined)[] }) {
  const shown = tags.filter((t): t is string => !!t);
  if (shown.length === 0) return null;
  return (
    <div className="mt-3 flex flex-wrap gap-2">
      {shown.map((tag) => (
        <Badge key={tag} variant="outline" className="border-border text-muted-foreground">
          {tag}
        </Badge>
      ))}
    </div>
  );
}

function SentencePatternCard({ p }: { p: SentencePattern }) {
  return (
    <div className="rounded-lg border border-border p-4">
      <p className="text-sm font-medium">{p.patternName}</p>
      {p.exampleSentence ? (
        <p className="mt-2 flex gap-2 text-sm text-muted-foreground">
          <Quote className="mt-0.5 size-3.5 shrink-0" />
          {p.exampleSentence}
        </p>
      ) : null}
      {p.breakdownSteps ? <BreakdownSteps raw={p.breakdownSteps} /> : null}
      {p.variants ? (
        <p className="mt-2 text-xs leading-relaxed text-muted-foreground">
          <span className="font-medium">常见变体：</span>
          {p.variants}
        </p>
      ) : null}
      <TagRow tags={[p.domain, p.scenario, p.frequencyTag]} />
    </div>
  );
}

function VocabCard({ v }: { v: VocabExpression }) {
  return (
    <div className="rounded-lg border border-border p-4">
      <p className="flex flex-wrap items-center gap-2 text-sm font-medium">
        {v.englishExpr}
        {v.literalTranslatable === false ? (
          <span className="rounded bg-warning/15 px-1.5 py-0.5 text-[10px] font-medium text-warning-foreground">
            不可机械直译
          </span>
        ) : null}
      </p>
      {v.chineseEquiv ? <p className="mt-1 text-sm text-primary">{v.chineseEquiv}</p> : null}
      {v.contextNote ? (
        <p className="mt-2 text-xs leading-relaxed text-muted-foreground">{v.contextNote}</p>
      ) : null}
      <TagRow tags={[v.domain, v.scenario, v.frequencyTag]} />
    </div>
  );
}

const UNCATEGORIZED = "其他";

export function DeepLearningPage() {
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
      title="深入学习"
      description={question.data?.title}
      actions={
        <>
          {content.data ? (
            <Badge variant="outline" className="border-primary/30 text-primary">
              {content.data.wasCached ? "命中缓存内容" : "本次新生成"}
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
        <AiLoadingState
          status="pending"
          pendingHint="AI 正在生成参考译文与学习卡片，首次生成较慢"
        />
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
                      参考译文
                    </CardTitle>
                  </CardHeader>
                  <CardContent className="space-y-4">
                    <ArticleText text={content.data.referenceText} className="text-[15px]" />
                    {content.data.comparisonNotes ? (
                      <div className="rounded-lg border border-border bg-secondary/50 p-4 text-sm leading-relaxed">
                        <p className="mb-1 font-medium">对照要点</p>
                        <ComparisonNotes raw={content.data.comparisonNotes} />
                      </div>
                    ) : null}
                  </CardContent>
                </Card>

                <Card className="border-border shadow-none">
                  <CardHeader>
                    <CardTitle className="text-base">句型拆解</CardTitle>
                  </CardHeader>
                  <CardContent className="space-y-4">
                    {content.data.sentencePatterns.length === 0 ? (
                      <p className="text-sm text-muted-foreground">
                        本文没有特别值得拆解的长难句。
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
                <CardTitle className="text-base">词汇与表达</CardTitle>
              </CardHeader>
              <CardContent className="min-h-0 flex-1 space-y-5 overflow-y-auto">
                {vocabGroups.length === 0 ? (
                  <p className="text-sm text-muted-foreground">本文没有特别值得积累的表达。</p>
                ) : (
                  vocabGroups.map(([group, items]) => (
                    <div key={group} className="space-y-3">
                      <p className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
                        {group}
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
