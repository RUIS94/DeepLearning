"use client";

import { useCallback, useState } from "react";
import { useMutation, useQuery } from "@tanstack/react-query";
import { generateQuestion, listQuestions } from "@/lib/api/questions";
import { listCategories } from "@/lib/api/exam-config";
import { listWeakPoints } from "@/lib/api/weak-points";
import { useExamType } from "@/hooks/use-exam-config";
import { useCurrentUser } from "@/hooks/use-current-user";
import { Difficulty, WeakPointStatus } from "@/lib/types/enums";

/** "random" = 提交时从已有选项里随机取一个真实值发给后端。 */
export const RANDOM = "random";

const DIFFICULTY_VALUES = [Difficulty.easy, Difficulty.medium, Difficulty.hard];

export interface AiGenerateState {
  taskType: string;
  difficulty: string; // "0" | "1" | "2" | RANDOM
  categoryId: string; // <id> | RANDOM
  targetWeakPoints: boolean;
  seedIds: string[];
}

const INITIAL: AiGenerateState = {
  taskType: "0",
  difficulty: RANDOM,
  categoryId: RANDOM,
  targetWeakPoints: false,
  seedIds: [],
};

function pick<T>(arr: readonly T[]): T | undefined {
  return arr.length ? arr[Math.floor(Math.random() * arr.length)] : undefined;
}

/**
 * AI 出题的表单 + 提交状态。**必须由不会随 SidePanel 开关而卸载的父组件持有**(题库页),
 * 这样面板关掉再打开时表单和 pending 状态都还在,避免重复发起生成请求 —— 直到成功才 reset。
 */
export function useAiGenerate(onGenerated: (questionId: string) => void) {
  const examType = useExamType();
  const currentUser = useCurrentUser();
  const [state, setState] = useState<AiGenerateState>(INITIAL);

  const categories = useQuery({ queryKey: ["categories"], queryFn: listCategories });
  const weakPoints = useQuery({
    queryKey: ["weak-points", currentUser.data?.id, WeakPointStatus.active],
    queryFn: () => listWeakPoints(currentUser.data!.id, WeakPointStatus.active),
    enabled: !!currentUser.data,
  });
  const seeds = useQuery({
    queryKey: ["questions", "seeds"],
    queryFn: () => listQuestions({ isSeedReference: true }),
  });

  const mutation = useMutation({
    mutationFn: () => {
      const difficulty =
        state.difficulty === RANDOM ? pick(DIFFICULTY_VALUES)! : Number(state.difficulty);
      const categoryId =
        state.categoryId === RANDOM
          ? (pick((categories.data ?? []).map((c) => c.id)) ?? null)
          : state.categoryId;
      return generateQuestion({
        examTypeId: examType.data!.id,
        taskType: Number(state.taskType),
        difficulty,
        categoryId,
        seedQuestionIds: state.seedIds.length ? state.seedIds : null,
        createdBy: currentUser.data?.id ?? null,
        targetWeakPoints: state.targetWeakPoints,
      });
    },
    onSuccess: (question) => {
      setState(INITIAL);
      onGenerated(question.id);
    },
  });

  const set = useCallback(
    <K extends keyof AiGenerateState>(key: K, value: AiGenerateState[K]) =>
      setState((prev) => ({ ...prev, [key]: value })),
    [],
  );

  const toggleSeed = useCallback(
    (id: string) =>
      setState((prev) => ({
        ...prev,
        seedIds: prev.seedIds.includes(id)
          ? prev.seedIds.filter((s) => s !== id)
          : prev.seedIds.length >= 5
            ? prev.seedIds
            : [...prev.seedIds, id],
      })),
    [],
  );

  return {
    state,
    set,
    toggleSeed,
    mutation,
    categories,
    weakPoints,
    seeds,
    examTypeReady: !!examType.data?.id,
  };
}
