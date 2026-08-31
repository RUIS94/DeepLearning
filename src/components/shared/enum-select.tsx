"use client";

import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";

/**
 * 通用枚举下拉（方案 §8.1）：接收一张 Record<number,string> 标签表 + 当前值，
 * admin 与练习端所有“选难度/选任务类型/选状态”的地方都用它，不重复写 Select+SelectItem 循环。
 */
export function EnumSelect({
  labels,
  value,
  onChange,
  placeholder,
  allowAll,
  allLabel = "全部",
  className,
}: {
  labels: Record<number, string>;
  value: number | "all";
  onChange: (value: number | "all") => void;
  placeholder?: string;
  allowAll?: boolean;
  allLabel?: string;
  className?: string;
}) {
  return (
    <Select value={String(value)} onValueChange={(v) => onChange(v === "all" ? "all" : Number(v))}>
      <SelectTrigger className={className}>
        <SelectValue placeholder={placeholder} />
      </SelectTrigger>
      <SelectContent>
        {allowAll ? <SelectItem value="all">{allLabel}</SelectItem> : null}
        {Object.entries(labels).map(([v, label]) => (
          <SelectItem key={v} value={v}>
            {label}
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  );
}
