"use client";

import { Trash2 } from "lucide-react";
import {
  SelectableSourceText,
  type HighlightRange,
} from "@/components/practice/selectable-source-text";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Badge } from "@/components/ui/badge";

export interface TaskBAnnotatorItem {
  key: string;
  start: number;
  end: number;
  categoryLabel: string;
  correctedText: string;
}

export interface TaskBTaxonomyOption {
  value: string;
  label: string;
}

/**
 * Task B 划词标注交互外壳：answer-page（答题页,`TaskBAnnotation[]` + errorCategory 字符串
 * key,plain useState）与 import-question-panel（导入表单,RHF useFieldArray + errorTaxonomyId）
 * 共享同一套“划词 → 选错误类型 → 填正确译文 → 加入列表”交互与排版,但两边的数据模型、
 * 部分文案/label 展示确实不同——因此标注项/分类选项/文案均由调用方归一化后传入,组件本身
 * 不假设任何一侧的语义（代码复用扫描_07_优化计划.md §4.5/R-W-11,配合 §3.4 的
 * TaskBSeededErrorValidation）。
 */
export function TaskBAnnotator({
  sourceText,
  highlightTone,
  showSourceText = true,
  dragHintLabel,
  items,
  onRemoveItem,
  draft,
  onSelectRange,
  onCancelDraft,
  taxonomyOptions,
  selectedTaxonomyValue,
  onSelectedTaxonomyValueChange,
  errorTypeLabel,
  correctedText,
  onCorrectedTextChange,
  correctedTextLabel,
  correctedTextPlaceholder,
  selectionText,
  addButtonText,
  addButtonDisabled,
  cancelButtonText,
  onAdd,
  annotatedCountText,
  buttonType,
}: {
  sourceText: string;
  highlightTone: "flag" | "seed";
  /** import-question-panel 只在 flawedText 非空时才渲染划词区；answer-page 恒为 true。 */
  showSourceText?: boolean;
  /** import-question-panel 在划词区上方多一行提示 label；answer-page 没有。 */
  dragHintLabel?: string;
  items: TaskBAnnotatorItem[];
  onRemoveItem: (index: number) => void;
  draft: { start: number; end: number } | null;
  onSelectRange: (start: number, end: number) => void;
  onCancelDraft: () => void;
  taxonomyOptions: TaskBTaxonomyOption[];
  selectedTaxonomyValue: string;
  onSelectedTaxonomyValueChange: (value: string) => void;
  errorTypeLabel?: string;
  correctedText: string;
  onCorrectedTextChange: (value: string) => void;
  correctedTextLabel?: string;
  correctedTextPlaceholder?: string;
  /** 已按各自 i18n key 格式化好的选区提示文案(两边插值参数不同,由调用方拼)。 */
  selectionText: string;
  addButtonText: string;
  addButtonDisabled: boolean;
  cancelButtonText: string;
  onAdd: () => void;
  /** 已格式化好的“已标注 N 处”文案。 */
  annotatedCountText: string;
  /** import-question-panel 的按钮在 <form> 内,需要显式 type="button" 防止误触发提交。 */
  buttonType?: "button";
}) {
  const highlightRanges: HighlightRange[] = items.map((item) => ({
    positionStart: item.start,
    positionEnd: item.end,
    tone: highlightTone,
  }));

  const sourceTextNode = (
    <SelectableSourceText
      text={sourceText}
      highlightRanges={highlightRanges}
      onSelectRange={onSelectRange}
    />
  );

  return (
    <>
      {showSourceText ? (
        dragHintLabel ? (
          <div className="space-y-3">
            <Label>{dragHintLabel}</Label>
            {sourceTextNode}
          </div>
        ) : (
          sourceTextNode
        )
      ) : null}

      {draft ? (
        <div className="space-y-3 rounded-lg border border-primary/40 bg-primary/5 p-4">
          <p className="text-numeric text-xs text-muted-foreground">{selectionText}</p>
          <div className="space-y-2">
            {errorTypeLabel ? <Label>{errorTypeLabel}</Label> : null}
            <Select value={selectedTaxonomyValue} onValueChange={onSelectedTaxonomyValueChange}>
              <SelectTrigger>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {taxonomyOptions.map((opt) => (
                  <SelectItem key={opt.value} value={opt.value}>
                    {opt.label}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-2">
            {correctedTextLabel ? <Label>{correctedTextLabel}</Label> : null}
            <Input
              value={correctedText}
              onChange={(e) => onCorrectedTextChange(e.target.value)}
              placeholder={correctedTextPlaceholder}
            />
          </div>
          <div className="flex gap-2">
            <Button type={buttonType} size="sm" disabled={addButtonDisabled} onClick={onAdd}>
              {addButtonText}
            </Button>
            <Button type={buttonType} size="sm" variant="ghost" onClick={onCancelDraft}>
              {cancelButtonText}
            </Button>
          </div>
        </div>
      ) : null}

      <div className="space-y-2">
        <p className="text-numeric text-xs font-medium text-muted-foreground">
          {annotatedCountText}
        </p>
        {items.map((item, i) => (
          <div
            key={item.key}
            className="flex items-start justify-between gap-3 rounded-md border border-border p-3"
          >
            <div className="space-y-1 text-sm">
              <div className="flex items-center gap-2">
                <Badge variant="outline" className="border-accent/40 text-accent">
                  {item.categoryLabel}
                </Badge>
                <span className="text-numeric text-xs text-muted-foreground">
                  [{item.start}, {item.end})
                </span>
              </div>
              <p>
                <span className="line-through opacity-60">
                  {sourceText.slice(item.start, item.end)}
                </span>
                <span className="mx-1">→</span>
                <span className="text-primary">{item.correctedText}</span>
              </p>
            </div>
            <Button type={buttonType} size="icon" variant="ghost" onClick={() => onRemoveItem(i)}>
              <Trash2 className="size-4" />
            </Button>
          </div>
        ))}
      </div>
    </>
  );
}
