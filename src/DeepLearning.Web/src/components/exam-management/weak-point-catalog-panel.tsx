"use client";

import { type Ref, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  CrudTable,
  type CrudColumn,
  type CrudCreateHandle,
  type CrudField,
} from "@/components/admin/crud-table";
import {
  createWeakPointCatalogEntry,
  listWeakPointCatalog,
  mergeWeakPointCatalog,
  updateWeakPointCatalogEntry,
} from "@/lib/api/exam-config";
import {
  weakPointCatalogFormSchema,
  type WeakPointCatalogFormInput,
} from "@/lib/validation/admin";
import type { WeakPointCatalogEntry } from "@/lib/types/dtos";
import { WeakPointCatalogStatus, WeakPointCatalogStatusLabel } from "@/lib/types/enums";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { showToast } from "@/components/ui/toast";
import { ApiError } from "@/lib/api/fetcher";

const STATUS_OPTIONS = [
  { value: String(WeakPointCatalogStatus.active), label: "已启用" },
  { value: String(WeakPointCatalogStatus.proposed), label: "待审" },
  { value: String(WeakPointCatalogStatus.deprecated), label: "已退役" },
];

const columns: CrudColumn<WeakPointCatalogEntry>[] = [
  {
    key: "code",
    header: "code",
    render: (c) => <span className="font-mono text-xs">{c.code}</span>,
  },
  { key: "name", header: "名称", render: (c) => c.name },
  {
    key: "match",
    header: "规则匹配键",
    render: (c) =>
      c.defaultDimensionKey
        ? `${c.defaultDimensionKey}${c.defaultErrorCategory ? ` / ${c.defaultErrorCategory}` : ""}`
        : "—",
  },
  {
    key: "status",
    header: "状态",
    render: (c) => (
      <Badge
        variant="outline"
        className={
          c.status === WeakPointCatalogStatus.proposed
            ? "border-warning/40 text-warning-foreground"
            : c.status === WeakPointCatalogStatus.deprecated
              ? "border-border text-muted-foreground"
              : "border-accent/40 text-accent"
        }
      >
        {WeakPointCatalogStatusLabel[c.status]}
        {c.origin !== "seed" ? ` · ${c.origin === "auto" ? "自动" : "手动"}` : ""}
      </Badge>
    ),
  },
  { key: "description", header: "说明", render: (c) => c.description },
];

const fields: CrudField<WeakPointCatalogFormInput>[] = [
  { name: "code", label: "code", kind: "text", placeholder: "omission_hedging" },
  { name: "name", label: "名称", kind: "text", placeholder: "省略保留性/让步语气词" },
  { name: "description", label: "说明", kind: "textarea" },
  {
    name: "defaultDimensionKey",
    label: "默认评分维度 key（可选）",
    kind: "text",
    placeholder: "meaning_transfer",
    description: "规则分桶按此匹配;留空表示不参与规则匹配,仅靠 AI 分类或手动归类。",
  },
  {
    name: "defaultErrorCategory",
    label: "默认错误类别 key（可选）",
    kind: "text",
    placeholder: "unjustified_omission",
  },
  { name: "status", label: "状态", kind: "select", options: STATUS_OPTIONS },
];

const defaultValues: WeakPointCatalogFormInput = {
  code: "",
  name: "",
  description: "",
  defaultDimensionKey: "",
  defaultErrorCategory: "",
  status: String(WeakPointCatalogStatus.active),
};

export function WeakPointCatalogPanel({
  examTypeId,
  createRef,
}: {
  examTypeId: string;
  createRef?: Ref<CrudCreateHandle>;
}) {
  const queryClient = useQueryClient();
  const key = ["admin", "weak-point-catalog", examTypeId];
  const catalog = useQuery({
    queryKey: key,
    queryFn: () => listWeakPointCatalog(examTypeId),
  });

  return (
    <div className="space-y-4">
      <MergeControl examTypeId={examTypeId} entries={catalog.data ?? []} onMerged={() => queryClient.invalidateQueries({ queryKey: key })} />
      <CrudTable
      openCreateRef={createRef}
      hideCreate
      columns={columns}
      items={catalog.data}
      isLoading={catalog.isPending}
      loadError={catalog.error}
      getRowId={(c) => c.id}
      schema={weakPointCatalogFormSchema}
      fields={fields}
      defaultValues={defaultValues}
      dialogTitle="新建薄弱点种类"
      onCreate={(values) =>
        createWeakPointCatalogEntry(examTypeId, {
          code: values.code,
          name: values.name,
          description: values.description,
          defaultDimensionKey: values.defaultDimensionKey || null,
          defaultErrorCategory: values.defaultErrorCategory || null,
        })
      }
      toFormValues={(c) => ({
        code: c.code,
        name: c.name,
        description: c.description,
        defaultDimensionKey: c.defaultDimensionKey ?? "",
        defaultErrorCategory: c.defaultErrorCategory ?? "",
        status: String(c.status),
      })}
      onUpdate={(id, values) =>
        updateWeakPointCatalogEntry(examTypeId, id, {
          name: values.name,
          description: values.description,
          defaultDimensionKey: values.defaultDimensionKey || "",
          defaultErrorCategory: values.defaultErrorCategory || "",
          status: Number(values.status),
        })
      }
      onChanged={() => queryClient.invalidateQueries({ queryKey: key })}
      />
    </div>
  );
}

function MergeControl({
  examTypeId,
  entries,
  onMerged,
}: {
  examTypeId: string;
  entries: WeakPointCatalogEntry[];
  onMerged: () => void;
}) {
  const [fromId, setFromId] = useState("");
  const [toId, setToId] = useState("");
  const options = entries.filter((e) => e.status !== WeakPointCatalogStatus.deprecated);

  const merge = useMutation({
    mutationFn: () => mergeWeakPointCatalog(examTypeId, fromId, toId),
    onSuccess: (res) => {
      showToast({
        variant: "success",
        title: "已合并",
        description: `重指 ${res.repointedCount} 条、合并 ${res.mergedCount} 条学习者薄弱点;来源种类已退役。`,
      });
      setFromId("");
      setToId("");
      onMerged();
    },
    onError: (err) =>
      showToast({
        variant: "error",
        title: "合并失败",
        description: err instanceof ApiError ? (err.problem?.title ?? "") : "",
      }),
  });

  return (
    <div className="flex flex-wrap items-end gap-3 rounded-lg border border-dashed border-border p-4">
      <div className="space-y-1">
        <p className="text-xs text-muted-foreground">合并(把「来源」并入「目标」,来源退役)</p>
        <div className="flex flex-wrap items-center gap-2">
          <Select value={fromId} onValueChange={setFromId}>
            <SelectTrigger className="h-9 w-52 text-sm">
              <SelectValue placeholder="来源种类" />
            </SelectTrigger>
            <SelectContent>
              {options.map((o) => (
                <SelectItem key={o.id} value={o.id}>
                  {o.name}（{o.code}）
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
          <span className="text-muted-foreground">→</span>
          <Select value={toId} onValueChange={setToId}>
            <SelectTrigger className="h-9 w-52 text-sm">
              <SelectValue placeholder="目标种类" />
            </SelectTrigger>
            <SelectContent>
              {options
                .filter((o) => o.id !== fromId)
                .map((o) => (
                  <SelectItem key={o.id} value={o.id}>
                    {o.name}（{o.code}）
                  </SelectItem>
                ))}
            </SelectContent>
          </Select>
        </div>
      </div>
      <Button
        variant="outline"
        disabled={!fromId || !toId || fromId === toId || merge.isPending}
        onClick={() => merge.mutate()}
      >
        合并
      </Button>
    </div>
  );
}
