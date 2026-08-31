"use client";

import Link from "next/link";
import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Sparkles } from "lucide-react";
import { AppShell } from "@/components/shared/app-shell";
import { QuestionCard } from "@/components/practice/question-card";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { listCategories } from "@/lib/api/exam-config";
import { listQuestions } from "@/lib/api/questions";
import { DifficultyLabel, TaskTypeLabel } from "@/lib/types/enums";

const ALL = "all";

export function PracticePage() {
  const [taskType, setTaskType] = useState(ALL);
  const [difficulty, setDifficulty] = useState(ALL);
  const [categoryId, setCategoryId] = useState(ALL);

  const categories = useQuery({ queryKey: ["categories"], queryFn: listCategories });
  const questions = useQuery({
    queryKey: ["questions", taskType, difficulty, categoryId],
    queryFn: () =>
      listQuestions({
        taskType: taskType === ALL ? undefined : Number(taskType),
        difficulty: difficulty === ALL ? undefined : Number(difficulty),
        categoryId: categoryId === ALL ? undefined : categoryId,
      }),
  });

  return (
    <AppShell
      title="题库"
      description="挑一道题开始练习。列表按后端约定一次性返回全部结果，暂无分页。"
      actions={
        <Button asChild>
          <Link href="/practice/generate">
            <Sparkles className="size-4" />
            AI 出题
          </Link>
        </Button>
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
          <SelectContent>
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
            <QuestionCard key={q.id} question={q} />
          ))}
        </div>
      ) : (
        <p className="rounded-lg border border-dashed border-border p-12 text-center text-sm text-muted-foreground">
          没有符合条件的题目，换个筛选条件或让 AI 生成一道。
        </p>
      )}
    </AppShell>
  );
}
