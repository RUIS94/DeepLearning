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
  listWeakPointCategories,
  mergeWeakPointCatalog,
  updateWeakPointCatalogEntry,
} from "@/lib/api/exam-config";
import { weakPointCatalogFormSchema, type WeakPointCatalogFormInput } from "@/lib/validation/admin";
import type { WeakPointCatalogEntry } from "@/lib/types/dtos";
import { WeakPointCatalogStatus } from "@/lib/types/enums";
import { useT } from "@/lib/i18n";
import { useEnumLabels } from "@/lib/i18n/enum-labels";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from "@/components/ui/dialog";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { showToast } from "@/components/ui/toast";
import { apiErrorMessage } from "@/lib/api/fetcher";
import { qk } from "@/lib/query-keys";

/** 薄弱点种类现在是全局共享的（不再按考试类型划分，见 策划书 §1.2），这个面板只是仍挂在考试配置页下展示。 */
export function WeakPointCatalogPanel({ createRef }: { createRef?: Ref<CrudCreateHandle> }) {
  const t = useT();
  const { WeakPointCatalogStatusLabel } = useEnumLabels();
  const STATUS_OPTIONS = [
    { value: String(WeakPointCatalogStatus.active), label: t("examMgmt.wpc.statusActive") },
    { value: String(WeakPointCatalogStatus.proposed), label: t("examMgmt.wpc.statusProposed") },
    {
      value: String(WeakPointCatalogStatus.deprecated),
      label: t("examMgmt.wpc.statusDeprecated"),
    },
  ];
  const queryClient = useQueryClient();
  const catalog = useQuery({
    queryKey: qk.adminWeakPointCatalog(),
    queryFn: () => listWeakPointCatalog(),
  });
  const categories = useQuery({
    queryKey: qk.adminWeakPointCategories(),
    queryFn: () => listWeakPointCategories(),
  });
  const categoryOptions = (categories.data ?? []).map((c) => ({ value: c.id, label: c.name }));

  const columns: CrudColumn<WeakPointCatalogEntry>[] = [
    { key: "name", header: t("common.name"), render: (c) => c.name },
    {
      key: "match",
      header: t("examMgmt.wpc.colRuleMatch"),
      render: (c) =>
        c.defaultDimensionKey
          ? `${c.defaultDimensionKey}${c.defaultErrorCategory ? ` / ${c.defaultErrorCategory}` : ""}`
          : "—",
    },
    {
      key: "status",
      header: t("common.status"),
      render: (c) => (
        <Badge
          variant="outline"
          className={
            c.status === WeakPointCatalogStatus.proposed
              ? "border-warning/40 text-warning-foreground"
              : c.status === WeakPointCatalogStatus.deprecated
                ? "border-destructive/40 text-destructive"
                : "border-success/40 text-success"
          }
        >
          {WeakPointCatalogStatusLabel[c.status]}
          {c.origin !== "seed"
            ? ` · ${c.origin === "auto" ? t("examMgmt.wpc.originAuto") : t("examMgmt.wpc.originManual")}`
            : ""}
        </Badge>
      ),
    },
    { key: "description", header: t("common.description"), render: (c) => c.description },
  ];

  const fields: CrudField<WeakPointCatalogFormInput>[] = [
    {
      name: "categoryId",
      label: t("examMgmt.wpc.fieldCategory"),
      kind: "select",
      options: categoryOptions,
    },
    { name: "code", label: "code", kind: "text", placeholder: "semantic_causality" },
    { name: "name", label: t("examMgmt.wpc.fieldName"), kind: "text", placeholder: "Causality" },
    { name: "description", label: t("examMgmt.wpc.fieldDescription"), kind: "textarea" },
    {
      name: "defaultDimensionKey",
      label: t("examMgmt.wpc.fieldDefaultDimension"),
      kind: "text",
      placeholder: "meaning_transfer",
      description: t("examMgmt.wpc.defaultDimensionHint"),
    },
    {
      name: "defaultErrorCategory",
      label: t("examMgmt.wpc.fieldDefaultErrorCategory"),
      kind: "text",
      placeholder: "unjustified_omission",
    },
    { name: "status", label: t("common.status"), kind: "select", options: STATUS_OPTIONS },
  ];

  const defaultValues: WeakPointCatalogFormInput = {
    categoryId: "",
    code: "",
    name: "",
    description: "",
    defaultDimensionKey: "",
    defaultErrorCategory: "",
    status: String(WeakPointCatalogStatus.active),
  };

  const invalidate = () => queryClient.invalidateQueries({ queryKey: qk.adminWeakPointCatalog() });

  const commonTableProps = {
    hideCreate: true as const,
    columns,
    isLoading: catalog.isPending || categories.isPending,
    loadError: catalog.error ?? categories.error,
    getRowId: (c: WeakPointCatalogEntry) => c.id,
    schema: weakPointCatalogFormSchema,
    fields,
    defaultValues,
    dialogTitle: t("examMgmt.wpc.dialogTitle"),
    onCreate: (values: WeakPointCatalogFormInput) =>
      createWeakPointCatalogEntry({
        categoryId: values.categoryId,
        code: values.code,
        name: values.name,
        description: values.description,
        defaultDimensionKey: values.defaultDimensionKey || null,
        defaultErrorCategory: values.defaultErrorCategory || null,
      }),
    toFormValues: (c: WeakPointCatalogEntry) => ({
      categoryId: c.categoryId ?? "",
      code: c.code,
      name: c.name,
      description: c.description,
      defaultDimensionKey: c.defaultDimensionKey ?? "",
      defaultErrorCategory: c.defaultErrorCategory ?? "",
      status: String(c.status),
    }),
    onUpdate: (id: string, values: WeakPointCatalogFormInput) =>
      updateWeakPointCatalogEntry(id, {
        name: values.name,
        description: values.description,
        defaultDimensionKey: values.defaultDimensionKey || "",
        defaultErrorCategory: values.defaultErrorCategory || "",
        status: Number(values.status),
      }),
    onChanged: invalidate,
  };

  // 按 8 大类拆表：先按种类列表顺序，未归类的行单独一张表垫底。
  const entries = catalog.data ?? [];
  const groups: { id: string; name: string; rows: WeakPointCatalogEntry[] }[] = [
    ...(categories.data ?? []).map((cat) => ({
      id: cat.id,
      name: cat.name,
      rows: entries.filter((e) => e.categoryId === cat.id),
    })),
    {
      id: "__uncategorized",
      name: t("examMgmt.wpc.categoryPending"),
      rows: entries.filter((e) => !e.categoryId),
    },
  ].filter((g) => g.rows.length > 0 || g.id !== "__uncategorized");

  return (
    <div className="space-y-6">
      {catalog.isPending || categories.isPending ? (
        <CrudTable {...commonTableProps} openCreateRef={createRef} items={undefined} />
      ) : (
        groups.map((group, i) => (
          <CrudTable
            key={group.id}
            {...commonTableProps}
            openCreateRef={i === 0 ? createRef : undefined}
            title={
              <div className="flex flex-1 items-center justify-between gap-4">
                <h3 className="text-sm font-semibold">{group.name}</h3>
                {i === 0 ? <MergeControl entries={entries} onMerged={invalidate} /> : null}
              </div>
            }
            items={group.rows}
          />
        ))
      )}
    </div>
  );
}

function MergeControl({
  entries,
  onMerged,
}: {
  entries: WeakPointCatalogEntry[];
  onMerged: () => void;
}) {
  const t = useT();
  const [open, setOpen] = useState(false);
  const [fromId, setFromId] = useState("");
  const [toId, setToId] = useState("");
  const options = entries.filter((e) => e.status !== WeakPointCatalogStatus.deprecated);

  const merge = useMutation({
    mutationFn: () => mergeWeakPointCatalog(fromId, toId),
    onSuccess: (res) => {
      showToast({
        variant: "success",
        title: t("examMgmt.wpc.merged"),
        description: t("examMgmt.wpc.mergedDesc", {
          repointed: res.repointedCount,
          merged: res.mergedCount,
        }),
      });
      setFromId("");
      setToId("");
      setOpen(false);
      onMerged();
    },
    onError: (err) =>
      showToast({
        variant: "error",
        title: t("examMgmt.wpc.mergeFailed"),
        description: apiErrorMessage(err),
      }),
  });

  return (
    <Dialog
      open={open}
      onOpenChange={(next) => {
        setOpen(next);
        if (!next) {
          setFromId("");
          setToId("");
        }
      }}
    >
      <DialogTrigger asChild>
        <Button variant="outline" size="sm">
          {t("examMgmt.wpc.merge")}
        </Button>
      </DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{t("examMgmt.wpc.mergeTitle")}</DialogTitle>
          <DialogDescription>{t("examMgmt.wpc.mergeHint")}</DialogDescription>
        </DialogHeader>
        <div className="flex flex-wrap items-center gap-2">
          <Select value={fromId} onValueChange={setFromId}>
            <SelectTrigger className="h-9 w-52 text-sm">
              <SelectValue placeholder={t("examMgmt.wpc.sourceCategory")} />
            </SelectTrigger>
            <SelectContent>
              {options.map((o) => (
                <SelectItem key={o.id} value={o.id}>
                  {o.name} ({o.code})
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
          <span className="text-muted-foreground">→</span>
          <Select value={toId} onValueChange={setToId}>
            <SelectTrigger className="h-9 w-52 text-sm">
              <SelectValue placeholder={t("examMgmt.wpc.targetCategory")} />
            </SelectTrigger>
            <SelectContent>
              {options
                .filter((o) => o.id !== fromId)
                .map((o) => (
                  <SelectItem key={o.id} value={o.id}>
                    {o.name} ({o.code})
                  </SelectItem>
                ))}
            </SelectContent>
          </Select>
        </div>
        <DialogFooter>
          <Button
            disabled={!fromId || !toId || fromId === toId || merge.isPending}
            onClick={() => merge.mutate()}
          >
            {t("examMgmt.wpc.merge")}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
