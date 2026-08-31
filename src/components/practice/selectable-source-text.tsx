"use client";

import { useState } from "react";
import { cn } from "@/lib/utils";

export interface HighlightRange {
  positionStart: number;
  positionEnd: number;
  tone?: "flag" | "seed" | "active";
  label?: string;
}

/**
 * 字符偏移选区组件（方案 9.2 节）。
 * 把文本渲染成逐字符 span，鼠标按下/抬起得到 [positionStart, positionEnd)，
 * 坐标系与后端 TaskBSeededError 的字符串索引完全一致。
 */
export function SelectableSourceText({
  text,
  highlightRanges = [],
  onSelectRange,
  readOnly = false,
  className,
}: {
  text: string;
  highlightRanges?: HighlightRange[];
  onSelectRange?: (positionStart: number, positionEnd: number) => void;
  readOnly?: boolean;
  className?: string;
}) {
  const [anchor, setAnchor] = useState<number | null>(null);
  const [hover, setHover] = useState<number | null>(null);

  const draft =
    anchor !== null && hover !== null
      ? { start: Math.min(anchor, hover), end: Math.max(anchor, hover) + 1 }
      : null;

  function toneFor(index: number): HighlightRange["tone"] | "draft" | undefined {
    if (draft && index >= draft.start && index < draft.end) return "draft";
    const hit = highlightRanges.find((r) => index >= r.positionStart && index < r.positionEnd);
    return hit?.tone ?? (hit ? "flag" : undefined);
  }

  return (
    <p
      className={cn("source-text select-none text-[15px] leading-9", className)}
      onMouseLeave={() => {
        setAnchor(null);
        setHover(null);
      }}
    >
      {Array.from(text).map((char, index) => {
        const tone = toneFor(index);
        return (
          <span
            key={index}
            data-index={index}
            onMouseDown={() => {
              if (readOnly) return;
              setAnchor(index);
              setHover(index);
            }}
            onMouseEnter={() => {
              if (readOnly || anchor === null) return;
              setHover(index);
            }}
            onMouseUp={() => {
              if (readOnly || anchor === null) return;
              const start = Math.min(anchor, index);
              const end = Math.max(anchor, index) + 1;
              setAnchor(null);
              setHover(null);
              if (end > start) onSelectRange?.(start, end);
            }}
            className={cn(
              "transition-colors",
              !readOnly && "cursor-text",
              tone === "draft" && "bg-primary/25",
              tone === "flag" && "bg-accent/20 underline decoration-accent decoration-wavy",
              tone === "seed" && "bg-success/20 underline decoration-success decoration-dotted",
              tone === "active" && "bg-warning/35",
            )}
          >
            {char}
          </span>
        );
      })}
    </p>
  );
}
