"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  listReviewPatterns,
  listReviewVocab,
  reviewPattern,
  reviewVocabItem,
} from "@/lib/api/review-library";

/**
 * 复习库（句型 + 词汇表达）共享的数据层：两个列表查询、两个「标记掌握程度」变更，
 * 以及给「题材」筛选用的 domain 并集。复习页顶部的固定筛选行与各 Tab 列表都用它，
 * 依赖 react-query 的 queryKey 去重，不会重复请求。
 */
export function useReviewLibrary(userId: string | undefined) {
  const queryClient = useQueryClient();

  const patterns = useQuery({
    queryKey: ["review-patterns", userId],
    queryFn: () => listReviewPatterns(userId!),
    enabled: !!userId,
  });
  const vocab = useQuery({
    queryKey: ["review-vocab", userId],
    queryFn: () => listReviewVocab(userId!),
    enabled: !!userId,
  });

  const markPattern = useMutation({
    mutationFn: (v: { id: string; level: number }) => reviewPattern(userId!, v.id, v.level),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["review-patterns"] }),
  });
  const markVocab = useMutation({
    mutationFn: (v: { id: string; level: number }) => reviewVocabItem(userId!, v.id, v.level),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["review-vocab"] }),
  });

  const domains = Array.from(
    new Set([...(patterns.data ?? []), ...(vocab.data ?? [])].map((i) => i.domain).filter(Boolean)),
  ) as string[];

  return { patterns, vocab, markPattern, markVocab, domains };
}
