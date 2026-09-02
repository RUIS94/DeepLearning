"use client";

import { useRouter } from "next/navigation";
import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Sparkles, Upload } from "lucide-react";
import { AppShell } from "@/components/shared/app-shell";
import { QuestionCard } from "@/components/practice/question-card";
import { AiGeneratePanel } from "@/components/practice/ai-generate-panel";
import { useImportPanel } from "@/components/practice/import-question-panel";
import { QuestionRecordsModal } from "@/components/practice/question-records-modal";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { showToast } from "@/components/ui/toast";
import { listCategories } from "@/lib/api/exam-config";
import { listQuestions } from "@/lib/api/questions";
import { useAiGenerate } from "@/hooks/use-ai-generate";
import { useCurrentUser } from "@/hooks/use-current-user";
import type { QuestionListItem } from "@/lib/types/dtos";
import { DifficultyLabel, TaskTypeLabel } from "@/lib/types/enums";

const ALL = "all";

export function PracticePage() {
  const router = useRouter();
  const importPanel = useImportPanel();
  const currentUser = useCurrentUser();
  const userId = currentUser.data?.id;

  const [taskType, setTaskType] = useState(ALL);
  const [difficulty, setDifficulty] = useState(ALL);
  const [categoryId, setCategoryId] = useState(ALL);
  const [genOpen, setGenOpen] = useState(false);
  const [recordsQuestion, setRecordsQuestion] = useState<QuestionListItem | null>(null);

  // useAiGenerate 挂在这个不会卸载的页面上,所以关掉面板不会丢表单/生成状态(见 hook 注释)。
  const gen = useAiGenerate((questionId) => {
    showToast({ variant: "success", title: "题目已生成", description: "正在进入答题页…" });
    setGenOpen(false);
    router.push(`/practice/${questionId}`);
  });

  const categories = useQuery({ queryKey: ["categories"], queryFn: listCategories });
  const questions = useQuery({
    queryKey: ["questions", taskType, difficulty, categoryId, userId],
    queryFn: () =>
      listQuestions({
        taskType: taskType === ALL ? undefined : Number(taskType),
        difficulty: difficulty === ALL ? undefined : Number(difficulty),
        categoryId: categoryId === ALL ? undefined : categoryId,
        userId,
      }),
  });

  return (
    <AppShell
      title="题库"
      description="选题并练习"
      actions={
        <>
          <Button variant="outline" onClick={() => importPanel.open()}>
            <Upload className="size-4" />
            导入题目
          </Button>
          <Button onClick={() => setGenOpen(true)}>
            <Sparkles className="size-4" />
            AI 出题
          </Button>
        </>
      }
    >
      <div className="mb-6 flex flex-wrap gap-3">
        <Select value={taskType} onValueChange={setTaskType}>
          <SelectTrigger className="w-44">
            <SelectValue placeholder="任务类型" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value={ALL}>全部任务类型</SelectItem>
            {Object.entries(TaskTypeLabel).map(([value, label]) => (
              <SelectItem key={value} value={value}>
                {label}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>

        <Select value={difficulty} onValueChange={setDifficulty}>
          <SelectTrigger className="w-36">
            <SelectValue placeholder="难度" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value={ALL}>全部难度</SelectItem>
            {Object.entries(DifficultyLabel).map(([value, label]) => (
              <SelectItem key={value} value={value}>
                {label}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>

        <Select value={categoryId} onValueChange={setCategoryId}>
          <SelectTrigger className="w-44">
            <SelectValue placeholder="题材分类" />
          </SelectTrigger>
          <SelectContent className="max-h-72">
            <SelectItem value={ALL}>全部分类</SelectItem>
            {(categories.data ?? []).map((c) => (
              <SelectItem key={c.id} value={c.id}>
                {c.name}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      {questions.isPending ? (
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {[0, 1, 2, 3, 4, 5].map((i) => (
            <Skeleton key={i} className="h-40 w-full rounded-xl" />
          ))}
        </div>
      ) : questions.data?.length ? (
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {questions.data.map((q) => (
            <QuestionCard key={q.id} question={q} onOpenRecords={setRecordsQuestion} />
          ))}
        </div>
      ) : (
        <p className="rounded-lg border border-dashed border-border p-12 text-center text-sm text-muted-foreground">
          没有符合条件的题目，换个筛选条件或让 AI 生成一道。
        </p>
      )}

      <AiGeneratePanel open={genOpen} onOpenChange={setGenOpen} gen={gen} />
      <QuestionRecordsModal
        open={recordsQuestion !== null}
        onOpenChange={(next) => {
          if (!next) setRecordsQuestion(null);
        }}
        questionId={recordsQuestion?.id ?? null}
        questionTitle={recordsQuestion?.title ?? ""}
        userId={userId}
      />
    </AppShell>
  );
}
