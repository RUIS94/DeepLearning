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
import { SkeletonList } from "@/components/shared/skeleton-list";
import { EmptyState } from "@/components/shared/empty-state";
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
import { useT } from "@/lib/i18n";
import { useEnumLabels } from "@/lib/i18n/enum-labels";
import type { QuestionListItem } from "@/lib/types/dtos";
import { qk } from "@/lib/query-keys";
import { enumOptions } from "@/lib/enum-options";

const ALL = "all";

export function PracticePage() {
  const t = useT();
  const { DifficultyLabel, TaskTypeLabel } = useEnumLabels();
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
    showToast({
      variant: "success",
      title: t("practice.generated.title"),
      description: t("practice.generated.description"),
    });
    setGenOpen(false);
    router.push(`/practice/${questionId}`);
  });

  const categories = useQuery({ queryKey: qk.categories(), queryFn: () => listCategories() });
  const questions = useQuery({
    queryKey: qk.questions(taskType, difficulty, categoryId, userId),
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
      title={t("practice.title")}
      description={t("practice.subtitle")}
      actions={
        <>
          <Button variant="outline" onClick={() => importPanel.open()}>
            <Upload className="size-4" />
            {t("practice.importQuestion")}
          </Button>
          <Button onClick={() => setGenOpen(true)}>
            <Sparkles className="size-4" />
            {t("practice.generateQuestion")}
          </Button>
        </>
      }
    >
      <div className="mb-6 flex flex-wrap gap-3">
        <Select value={taskType} onValueChange={setTaskType}>
          <SelectTrigger className="w-44">
            <SelectValue placeholder={t("practice.filter.taskType")} />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value={ALL}>{t("practice.filter.allTaskTypes")}</SelectItem>
            {enumOptions(TaskTypeLabel).map((o) => (
              <SelectItem key={o.value} value={o.value}>
                {o.label}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>

        <Select value={difficulty} onValueChange={setDifficulty}>
          <SelectTrigger className="w-36">
            <SelectValue placeholder={t("practice.filter.difficulty")} />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value={ALL}>{t("practice.filter.allDifficulties")}</SelectItem>
            {enumOptions(DifficultyLabel).map((o) => (
              <SelectItem key={o.value} value={o.value}>
                {o.label}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>

        <Select value={categoryId} onValueChange={setCategoryId}>
          <SelectTrigger className="w-44">
            <SelectValue placeholder={t("practice.filter.categories")} />
          </SelectTrigger>
          <SelectContent className="max-h-72">
            <SelectItem value={ALL}>{t("practice.filter.allCategories")}</SelectItem>
            {(categories.data ?? []).map((c) => (
              <SelectItem key={c.id} value={c.id}>
                {c.name}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      {questions.isPending ? (
        <SkeletonList
          count={6}
          itemClassName="h-40 rounded-xl"
          containerClassName="grid gap-4 sm:grid-cols-2 lg:grid-cols-3"
        />
      ) : questions.data?.length ? (
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {questions.data.map((q) => (
            <QuestionCard key={q.id} question={q} onOpenRecords={setRecordsQuestion} />
          ))}
        </div>
      ) : (
        <EmptyState>{t("practice.empty")}</EmptyState>
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
