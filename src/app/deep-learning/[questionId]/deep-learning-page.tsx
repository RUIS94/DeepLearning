"use client";

import { useParams } from "next/navigation";
import { useQuery } from "@tanstack/react-query";
import { BookOpenCheck, Quote } from "lucide-react";
import { AppShell } from "@/components/shared/app-shell";
import { ArticleText } from "@/components/shared/article-text";
import { AiLoadingState, ErrorBanner } from "@/components/shared/ai-loading-state";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { generateDeepLearning } from "@/lib/api/deep-learning";
import { getQuestionById } from "@/lib/api/questions";
import { useExamType } from "@/hooks/use-exam-config";

export function DeepLearningPage() {
  const { questionId } = useParams<{ questionId: string }>();
  const examType = useExamType();
  const question = useQuery({
    queryKey: ["question", questionId],
    queryFn: () => getQuestionById(questionId),
  });
  const content = useQuery({
    queryKey: ["deep-learning", questionId],
    queryFn: () => generateDeepLearning(questionId, examType.data!.id),
    enabled: !!examType.data,
  });

  return (
    <AppShell
      title="深入学习"
      description={question.data?.title}
      actions={
        content.data ? (
          <Badge variant="outline" className="border-primary/30 text-primary">
            {content.data.wasCached ? "命中缓存内容" : "本次新生成"}
          </Badge>
        ) : null
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
        <div className="grid gap-6 lg:grid-cols-[1fr_380px]">
          <div className="space-y-6">
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
                    {content.data.comparisonNotes}
                  </div>
                ) : null}
              </CardContent>
            </Card>

            <Card className="border-border shadow-none">
              <CardHeader>
                <CardTitle className="text-base">句型拆解</CardTitle>
              </CardHeader>
              <CardContent className="space-y-4">
                {content.data.sentencePatterns.map((p) => (
                  <div key={p.id} className="rounded-lg border border-border p-4">
                    <p className="text-sm font-medium">{p.patternName}</p>
                    {p.exampleSentence ? (
                      <p className="mt-2 flex gap-2 text-sm text-muted-foreground">
                        <Quote className="mt-0.5 size-3.5 shrink-0" />
                        {p.exampleSentence}
                      </p>
                    ) : null}
                    {p.breakdownSteps ? (
                      <p className="mt-2 text-sm leading-relaxed">{p.breakdownSteps}</p>
                    ) : null}
                    <div className="mt-3 flex flex-wrap gap-2">
                      {[p.domain, p.scenario, p.frequencyTag].filter(Boolean).map((tag) => (
                        <Badge
                          key={tag}
                          variant="outline"
                          className="border-border text-muted-foreground"
                        >
                          {tag}
                        </Badge>
                      ))}
                    </div>
                  </div>
                ))}
              </CardContent>
            </Card>
          </div>

          <Card className="h-fit border-border shadow-none">
            <CardHeader>
              <CardTitle className="text-base">词汇与表达</CardTitle>
            </CardHeader>
            <CardContent className="space-y-4">
              {content.data.vocabExpressions.map((v) => (
                <div key={v.id} className="rounded-lg border border-border p-4">
                  <p className="text-sm font-medium">{v.englishExpr}</p>
                  <p className="mt-1 text-sm text-primary">{v.chineseEquiv}</p>
                  {v.contextNote ? (
                    <p className="mt-2 text-xs leading-relaxed text-muted-foreground">
                      {v.contextNote}
                    </p>
                  ) : null}
                  <div className="mt-3 flex flex-wrap gap-2">
                    {[v.domain, v.frequencyTag].filter(Boolean).map((tag) => (
                      <Badge
                        key={tag}
                        variant="outline"
                        className="border-border text-muted-foreground"
                      >
                        {tag}
                      </Badge>
                    ))}
                  </div>
                </div>
              ))}
            </CardContent>
          </Card>
        </div>
      ) : null}
    </AppShell>
  );
}
