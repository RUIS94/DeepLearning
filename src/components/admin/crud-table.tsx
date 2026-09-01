"use client";

import { Fragment, useState, type ReactNode } from "react";
import {
  Controller,
  useForm,
  type DefaultValues,
  type FieldValues,
  type Path,
  type UseFormReturn,
} from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import type { ZodType } from "zod";
import { ChevronDown, ChevronRight, Pencil, PlusCircle, Trash2 } from "lucide-react";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { ConfirmDialog } from "@/components/ui/confirm-dialog";
import { showToast } from "@/components/ui/toast";
import { ApiError } from "@/lib/api/fetcher";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { Label } from "@/components/ui/label";
import { Switch } from "@/components/ui/switch";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { Skeleton } from "@/components/ui/skeleton";
import { ErrorBanner } from "@/components/shared/ai-loading-state";

/**
 * 通用后台增改列表（方案 §8.2）。列定义 + 表单 schema 作为 props，不为每个资源重复写表格+弹窗。
 * 可选支持:
 * - onUpdate + toFormValues：每行出现「编辑」按钮，复用同一套 fields 弹窗做修改
 * - onDelete：每行出现「删除」按钮（走 ConfirmDialog；后端 409 等错误用 toast 提示）
 * - renderExpanded：行可展开，展开区跨整行（题库评分维度的 level_descriptions 用）
 */

export interface CrudColumn<TItem> {
  key: string;
  header: string;
  render: (item: TItem) => ReactNode;
  className?: string;
}

export type CrudField<TFormValues extends FieldValues> =
  | {
      name: Path<TFormValues>;
      label: string;
      kind: "text";
      placeholder?: string;
      description?: string;
    }
  | {
      name: Path<TFormValues>;
      label: string;
      kind: "textarea";
      placeholder?: string;
      rows?: number;
      description?: string;
    }
  | { name: Path<TFormValues>; label: string; kind: "number"; description?: string }
  | { name: Path<TFormValues>; label: string; kind: "date"; description?: string }
  | { name: Path<TFormValues>; label: string; kind: "switch"; description?: string }
  | {
      name: Path<TFormValues>;
      label: string;
      kind: "select";
      options: { value: string; label: string }[];
      valueType?: "number" | "string";
      description?: string;
    };

type Mode<TItem> = "closed" | "create" | { edit: TItem };

export function CrudTable<TItem, TFormValues extends FieldValues>({
  columns,
  items,
  isLoading,
  loadError,
  getRowId,
  schema,
  fields,
  defaultValues,
  onCreate,
  onCreated,
  onUpdate,
  toFormValues,
  onDelete,
  onChanged,
  renderExpanded,
  deleteConfirm,
  dialogTitle = "新建",
  editDialogTitle = "编辑",
  createButtonLabel = "新建",
  emptyMessage = "暂无数据",
  hideCreate = false,
}: {
  columns: CrudColumn<TItem>[];
  items: TItem[] | undefined;
  isLoading: boolean;
  loadError?: unknown;
  getRowId: (item: TItem) => string;
  schema: ZodType<TFormValues>;
  fields: CrudField<TFormValues>[];
  defaultValues: DefaultValues<TFormValues>;
  onCreate: (values: TFormValues) => Promise<TItem>;
  onCreated?: (item: TItem) => void;
  onUpdate?: (id: string, values: TFormValues) => Promise<unknown>;
  toFormValues?: (item: TItem) => TFormValues;
  onDelete?: (id: string) => Promise<unknown>;
  onChanged?: () => void;
  renderExpanded?: (item: TItem) => ReactNode;
  deleteConfirm?: (item: TItem) => { title: string; description?: string };
  dialogTitle?: string;
  editDialogTitle?: string;
  createButtonLabel?: string;
  emptyMessage?: string;
  hideCreate?: boolean;
}) {
  const [mode, setMode] = useState<Mode<TItem>>("closed");
  const [submitError, setSubmitError] = useState<unknown>(null);
  const [deleting, setDeleting] = useState<TItem | null>(null);
  const [expandedId, setExpandedId] = useState<string | null>(null);
  const form = useForm<TFormValues>({ resolver: zodResolver(schema), defaultValues });
  const errors = form.formState.errors as Record<string, { message?: string } | undefined>;

  const canEdit = !!onUpdate && !!toFormValues;
  const actionsColumn = canEdit || !!onDelete;

  function openCreate() {
    form.reset(defaultValues);
    setSubmitError(null);
    setMode("create");
  }
  function openEdit(item: TItem) {
    form.reset(toFormValues!(item));
    setSubmitError(null);
    setMode({ edit: item });
  }
  function closeDialog() {
    setMode("closed");
    form.reset(defaultValues);
    setSubmitError(null);
  }

  async function handleSubmit(values: TFormValues) {
    setSubmitError(null);
    try {
      if (mode !== "closed" && mode !== "create") {
        await onUpdate!(getRowId(mode.edit), values);
      } else {
        const created = await onCreate(values);
        onCreated?.(created);
      }
      onChanged?.();
      closeDialog();
    } catch (err) {
      setSubmitError(err);
    }
  }

  async function confirmDelete() {
    if (!deleting) return;
    try {
      await onDelete!(getRowId(deleting));
      onChanged?.();
      setDeleting(null);
    } catch (err) {
      const msg =
        err instanceof ApiError ? (err.problem?.title ?? `删除失败（${err.status}）`) : "删除失败";
      showToast({ variant: "error", title: "无法删除", description: msg });
      throw err; // 让 ConfirmDialog 保持打开
    }
  }

  return (
    <div className="space-y-4">
      {!hideCreate ? (
        <div className="flex justify-end">
          <Button onClick={openCreate}>
            <PlusCircle className="size-4" />
            {createButtonLabel}
          </Button>
        </div>
      ) : null}

      <Dialog open={mode !== "closed"} onOpenChange={(next) => (next ? null : closeDialog())}>
        <DialogContent className="max-h-[85vh] max-w-lg overflow-y-auto">
          <DialogHeader>
            <DialogTitle>
              {mode !== "closed" && mode !== "create" ? editDialogTitle : dialogTitle}
            </DialogTitle>
          </DialogHeader>
          <form className="space-y-4" onSubmit={form.handleSubmit(handleSubmit)}>
            {fields.map((field) => (
              <div key={field.name} className="space-y-2">
                {field.kind !== "switch" ? <Label htmlFor={field.name}>{field.label}</Label> : null}
                <CrudFieldControl field={field} form={form} />
                {field.description ? (
                  <p className="text-xs text-muted-foreground">{field.description}</p>
                ) : null}
                {errors[field.name]?.message ? (
                  <p className="text-xs text-destructive">{errors[field.name]!.message}</p>
                ) : null}
              </div>
            ))}
            {submitError ? <ErrorBanner error={submitError} /> : null}
            <DialogFooter>
              <Button type="submit" disabled={form.formState.isSubmitting}>
                {form.formState.isSubmitting ? "提交中…" : "确认"}
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>

      {isLoading ? (
        <Skeleton className="h-48 w-full rounded-xl" />
      ) : loadError ? (
        <ErrorBanner error={loadError} />
      ) : items?.length ? (
        <div className="overflow-x-auto rounded-xl border border-border">
          <Table>
            <TableHeader>
              <TableRow>
                {renderExpanded ? <TableHead className="w-8" /> : null}
                {columns.map((c) => (
                  <TableHead key={c.key}>{c.header}</TableHead>
                ))}
                {actionsColumn ? <TableHead className="w-24 text-right">操作</TableHead> : null}
              </TableRow>
            </TableHeader>
            <TableBody>
              {items.map((item) => {
                const id = getRowId(item);
                const expanded = expandedId === id;
                const colCount =
                  columns.length + (renderExpanded ? 1 : 0) + (actionsColumn ? 1 : 0);
                return (
                  <Fragment key={id}>
                    <TableRow>
                      {renderExpanded ? (
                        <TableCell className="w-8 align-top">
                          <button
                            type="button"
                            onClick={() => setExpandedId(expanded ? null : id)}
                            className="text-muted-foreground transition-colors hover:text-foreground"
                            aria-label={expanded ? "收起" : "展开"}
                          >
                            {expanded ? (
                              <ChevronDown className="size-4" />
                            ) : (
                              <ChevronRight className="size-4" />
                            )}
                          </button>
                        </TableCell>
                      ) : null}
                      {columns.map((c) => (
                        <TableCell key={c.key} className={c.className}>
                          {c.render(item)}
                        </TableCell>
                      ))}
                      {actionsColumn ? (
                        <TableCell className="text-right">
                          <div className="flex justify-end gap-1">
                            {canEdit ? (
                              <Button
                                type="button"
                                size="icon"
                                variant="ghost"
                                className="size-8"
                                onClick={() => openEdit(item)}
                              >
                                <Pencil className="size-3.5" />
                              </Button>
                            ) : null}
                            {onDelete ? (
                              <Button
                                type="button"
                                size="icon"
                                variant="ghost"
                                className="size-8 text-muted-foreground hover:text-destructive"
                                onClick={() => setDeleting(item)}
                              >
                                <Trash2 className="size-3.5" />
                              </Button>
                            ) : null}
                          </div>
                        </TableCell>
                      ) : null}
                    </TableRow>
                    {renderExpanded && expanded ? (
                      <TableRow>
                        <TableCell colSpan={colCount} className="bg-muted/40">
                          {renderExpanded(item)}
                        </TableCell>
                      </TableRow>
                    ) : null}
                  </Fragment>
                );
              })}
            </TableBody>
          </Table>
        </div>
      ) : (
        <p className="rounded-lg border border-dashed border-border p-10 text-center text-sm text-muted-foreground">
          {emptyMessage}
        </p>
      )}

      {onDelete ? (
        <ConfirmDialog
          open={deleting !== null}
          onOpenChange={(next) => {
            if (!next) setDeleting(null);
          }}
          tone="warning"
          title={deleting && deleteConfirm ? deleteConfirm(deleting).title : "确认删除？"}
          description={
            deleting && deleteConfirm ? deleteConfirm(deleting).description : "此操作不可撤销。"
          }
          confirmLabel="删除"
          onConfirm={confirmDelete}
        />
      ) : null}
    </div>
  );
}

function CrudFieldControl<TFormValues extends FieldValues>({
  field,
  form,
}: {
  field: CrudField<TFormValues>;
  form: UseFormReturn<TFormValues>;
}) {
  if (field.kind === "text" || field.kind === "date") {
    return (
      <Input
        id={field.name}
        type={field.kind === "date" ? "date" : "text"}
        placeholder={field.kind === "text" ? field.placeholder : undefined}
        {...form.register(field.name)}
      />
    );
  }
  if (field.kind === "textarea") {
    return (
      <Textarea
        id={field.name}
        rows={field.rows ?? 4}
        placeholder={field.placeholder}
        {...form.register(field.name)}
      />
    );
  }
  if (field.kind === "number") {
    return (
      <Input
        id={field.name}
        type="number"
        {...form.register(field.name, { valueAsNumber: true })}
      />
    );
  }
  if (field.kind === "switch") {
    return (
      <Controller
        control={form.control}
        name={field.name}
        render={({ field: { value, onChange } }) => (
          <div className="flex items-center justify-between rounded-lg border border-border p-3">
            <Label htmlFor={field.name}>{field.label}</Label>
            <Switch id={field.name} checked={Boolean(value)} onCheckedChange={onChange} />
          </div>
        )}
      />
    );
  }
  return (
    <Controller
      control={form.control}
      name={field.name}
      render={({ field: { value, onChange } }) => (
        <Select
          {...(value === null || value === undefined ? {} : { value: String(value) })}
          onValueChange={(v) => onChange(field.valueType === "number" ? Number(v) : v)}
        >
          <SelectTrigger id={field.name}>
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            {field.options.map((o) => (
              <SelectItem key={o.value} value={o.value}>
                {o.label}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      )}
    />
  );
}
