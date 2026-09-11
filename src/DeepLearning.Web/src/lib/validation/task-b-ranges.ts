/**
 * Task B 标注区间校验的共享逻辑——镜像后端 TaskBSeededErrorValidation（代码复用扫描_07_优化计划.md
 * §3.4/N10）：端序（end > start）、落在文本长度内、排序后相邻不重叠。之前 submission.ts 的
 * taskBAnnotationSchema 和 question-import.ts 的 importUserQuestionSchema 各自内联一份端序/越界
 * 检查，只有 findOverlappingAnnotations 是共享的；现在三条规则都从这里出。
 */

export function isValidRange(positionStart: number, positionEnd: number): boolean {
  return positionEnd > positionStart;
}

export function isWithinBounds(
  positionStart: number,
  positionEnd: number,
  textLength: number,
): boolean {
  return positionStart >= 0 && positionEnd <= textLength;
}

/** 校验一组标注互不重叠（排序后相邻检查，与顺序无关）。 */
export function findOverlappingAnnotations(
  annotations: { positionStart: number; positionEnd: number }[],
) {
  const sorted = [...annotations].sort((a, b) => a.positionStart - b.positionStart);
  for (let i = 1; i < sorted.length; i++) {
    if (sorted[i]!.positionStart < sorted[i - 1]!.positionEnd) {
      return [sorted[i - 1], sorted[i]] as const;
    }
  }
  return null;
}
