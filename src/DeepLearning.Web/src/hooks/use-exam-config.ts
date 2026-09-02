"use client";

import { useQuery } from "@tanstack/react-query";
import { listErrorTaxonomiesByExamType, listExamTypes } from "@/lib/api/exam-config";

/**
 * 应用目前只服务单一考试类型（NAATI CT）——取 /exam-types 列表第一条当作全局"当前 examType"
 * （方案 §9.1"ExamTypeId 的全局引导"）。以前是 `lib/mock/store.ts` 里一个同步导出的常量，
 * 接真实后端后数据只能异步拿到，所以换成 hook；真的要支持多考试类型切换时，这里要换成读
 * 用户选择 / URL 参数，而不是硬取第一条。
 */
export function useExamType() {
  return useQuery({
    queryKey: ["exam-config", "exam-type"],
    queryFn: async () => {
      const list = await listExamTypes();
      return list[0] ?? null;
    },
  });
}

export function useErrorTaxonomies(examTypeId: string | undefined) {
  return useQuery({
    queryKey: ["exam-config", "error-taxonomies", examTypeId],
    queryFn: () => listErrorTaxonomiesByExamType(examTypeId!),
    enabled: !!examTypeId,
  });
}
