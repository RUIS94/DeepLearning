"use client";

import { useRouter } from "next/navigation";
import { useState } from "react";
import { useMutation, useQuery } from "@tanstack/react-query";
import { Sparkles } from "lucide-react";
import { AppShell } from "@/components/shared/app-shell";
import { AiLoadingState } from "@/components/shared/ai-loading-state";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Label } from "@/components/ui/label";
import { Switch } from "@/components/ui/switch";
import { Badge } from "@/components/ui/badge";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { generateQuestion, listQuestions } from "@/lib/api/questions";
import { listCategories } from "@/lib/api/exam-config";
import { listWeakPoints } from "@/lib/api/weak-points";
import { useExamType } from "@/hooks/use-exam-config";
import { useCurrentUser } from "@/hooks/use-current-user";
import { DifficultyLabel, PriorityLabel, TaskTypeLabel, WeakPointStatus } from "@/lib/types/enums";
import { generateQuestionFormSchema } from "@/lib/validation/question-generate";

export function GeneratePage() {
  const router = useRouter();
  const [taskType, setTaskType] = useState("0");
  const [difficulty, setDifficulty] = useState("1");
  const [categoryId, setCategoryId] = useState("cat-health");
  const [targetWeakPoints, setTargetWeakPoints] = useState(true);
  const [seedIds, setSeedIds] = useState<string[]>([]);

  const examType = useExamType();
  const currentUser = useCurrentUser();
  const categories = useQuery({ queryKey: ["categories"], queryFn: listCategories });
  const weakPoints = useQuery({
    queryKey: ["weak-points", currentUser.data?.id, WeakPointStatus.active],
    queryFn: () => listWeakPoints(currentUser.data!.id, WeakPointStatus.active),
    enabled: !!currentUser.data,
  });
  const seeds = useQuery({
    queryKey: ["questions", "seeds"],
    queryFn: () => listQuestions({ inBank: true }),
  });

  // 镜像后端 GenerateQuestionCommand 校验规则（方案 §11）：seedQuestionIds 最多 5 个、不能重复。
  const requestValidation = generateQuestionFormSchema.safeParse({
    examTypeId: examType.data?.id ?? "",
    taskType: Number(taskType),
    difficulty: Number(difficulty),
    categoryId,
    seedQuestionIds: seedIds.length ? seedIds : null,
    targetWeakPoints,
  });

  const mutation = useMutation({
    mutationFn: () =>
      generateQuestion({
        examTypeId: examType.data!.id,
        taskType: Number(taskType),
        difficulty: Number(difficulty),
        categoryId,
        seedQuestionIds: seedIds.length ? seedIds : null,
        createdBy: currentUser.data?.id ?? null,
        targetWeakPoints,
      }),
    onSuccess: (question) => router.push(`/practice/${question.id}`),
  });

  function toggleSeed(id: string) {
    setSeedIds((prev) =>
      prev.includes(id) ? prev.filter((s) => s !== id) : prev.length >= 5 ? prev : [...prev, id],
    );
  }

  return (
    <AppShell
      title="AI 出题"
      description="生成是慢请求，最长可能十几秒；生成完成后会直接进入答题页。"
    >
      <div className="grid gap-6 lg:grid-cols-[1fr_360px]">
        <Card className="border-border shadow-none">
          <CardHeader>
            <CardTitle className="text-base">出题配置</CardTitle>
          </CardHeader>
          <CardContent className="space-y-6">
            <div className="grid gap-4 sm:grid-cols-3">
              <div className="space-y-2">
                <Label>任务类型</Label>
                <Select value={taskType} onValueChange={setTaskType}>
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {Object.entries(TaskTypeLabel).map(([v, l]) => (
                      <SelectItem key={v} value={v}>
                        {l}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-2">
                <Label>难度</Label>
                <Select value={difficulty} onValueChange={setDifficulty}>
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {Object.entries(DifficultyLabel).map(([v, l]) => (
                      <SelectItem key={v} value={v}>
                        {l}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-2">
                <Label>题材分类</Label>
                <Select value={categoryId} onValueChange={setCategoryId}>
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {(categories.data ?? []).map((c) => (
                      <SelectItem key={c.id} value={c.id}>
                        {c.name}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            </div>

            <div className="flex items-start justify-between gap-4 rounded-lg border border-border p-4">
              <div>
                <p className="text-sm font-medium">命中薄弱点</p>
                <p className="mt-1 text-xs text-muted-foreground">
                  让 AI 优先围绕当前高优先级薄弱点设计题目。
                </p>
              </div>
              <Switch checked={targetWeakPoints} onCheckedChange={setTargetWeakPoints} />
            </div>

            <div className="space-y-2">
              <Label>真题种子（可选，最多 5 道）</Label>
              <div className="flex flex-wrap gap-2">
                {(seeds.data ?? []).map((q) => (
                  <button
                    key={q.id}
                    type="button"
                    onClick={() => toggleSeed(q.id)}
                    className={`rounded-full border px-3 py-1.5 text-xs transition-colors ${
                      seedIds.includes(q.id)
                        ? "border-primary bg-primary text-primary-foreground"
                        : "border-border text-muted-foreground hover:bg-secondary"
                    }`}
                  >
                    {q.title}
                  </button>
                ))}
              </div>
              <p className="text-numeric text-xs text-muted-foreground">
                已选 {seedIds.length} / 5
              </p>
            </div>

            <div className="space-y-3">
              <Button
                disabled={mutation.isPending || !requestValidation.success}
                onClick={() => mutation.mutate()}
              >
                <Sparkles className="size-4" />
                {mutation.isPending ? "生成中…" : "生成题目"}
              </Button>
              {!requestValidation.success ? (
                <p className="text-xs text-destructive">
                  {requestValidation.error.issues[0]?.message}
                </p>
              ) : null}
              <AiLoadingState
                status={mutation.status}
                error={mutation.error}
                pendingHint="AI 正在出题，可能需要几秒到十几秒"
              />
            </div>
          </CardContent>
        </Card>

        <Card className="h-fit border-border shadow-none">
          <CardHeader>
            <CardTitle className="text-base">当前活跃薄弱点</CardTitle>
          </CardHeader>
          <CardContent className="space-y-3">
            {(weakPoints.data ?? []).map((w) => (
              <div key={w.id} className="rounded-lg border border-border p-3">
                <div className="flex items-center justify-between gap-2">
                  <p className="text-sm font-medium">{w.category}</p>
                  <Badge variant="outline" className="border-accent/40 text-accent">
                    {PriorityLabel[w.priority]}优先
                  </Badge>
                </div>
                <p className="text-numeric mt-1 text-xs text-muted-foreground">
                  累计出现 {w.recurrenceCount} 次
                </p>
              </div>
            ))}
          </CardContent>
        </Card>
      </div>
    </AppShell>
  );
}
