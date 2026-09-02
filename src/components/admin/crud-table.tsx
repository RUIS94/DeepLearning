"use client";

import { Fragment, useImperativeHandle, useState, type ReactNode, type Ref } from "react";
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

/** 通过 ref 从外部（如把「新建」按钮提到 Tab 同行时）打开新建弹窗。 */
export interface CrudCreateHandle {
  openCreate: () => void;
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
  lockOnEdit,
  title,
  dialogTitle = "新建",
  editDialogTitle = "编辑",
  createButtonLabel = "新建",
  emptyMessage = "暂无数据",
  hideCreate = false,
  openCreateRef,
  dialogOnly = false,
}: {
  /** 表格上方的标题（如分组名），与「新建」按钮同行显示；不传则只显示按钮（旧行为）。 */
  title?: ReactNode;
  columns?: CrudColumn<TItem>[];
  items?: TItem[] | undefined;
  isLoading: boolean;
  loadError?: unknown;
  getRowId?: (item: TItem) => string;
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
  /**
   * 编辑时禁用（置灰）的字段名。后端 PUT 只更新部分字段时用——否则用户改了这些字段、
   * 点保存、什么都没发生，很困惑（见 prompt-templates-panel 的 examTypeId/layer 等）。
   */
  lockOnEdit?: Path<TFormValues>[];
  dialogTitle?: string;
  editDialogTitle?: string;
  createButtonLabel?: string;
  emptyMessage?: string;
  hideCreate?: boolean;
  /** 暴露「打开新建弹窗」给外部调用（把新建按钮提到 Tab 同行时用）。 */
  openCreateRef?: Ref<CrudCreateHandle> | undefined;
  /** 只渲染新建弹窗，不渲染表格与按钮（配合 openCreateRef，做一个纯弹窗入口）。 */
  dialogOnly?: boolean;
}) {
  const cols = columns ?? [];
  const rowId = getRowId ?? (() => "");
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
  useImperativeHandle(openCreateRef, () => ({ openCreate }));
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
    const editing = mode !== "closed" && mode !== "create";
    try {
      if (editing) {
        await onUpdate!(rowId(mode.edit), values);
      } else {
        const created = await onCreate(values);
        onCreated?.(created);
      }
      onChanged?.();
      closeDialog();
      showToast({ variant: "success", title: editing ? "已保存" : "已创建" });
    } catch (err) {
      setSubmitError(err);
      const msg =
        err instanceof ApiError ? (err.problem?.title ?? `保存失败（${err.status}）`) : "保存失败";
      showToast({ variant: "error", title: "无法保存", description: msg });
    }
  }

  // zodResolver 校验不通过时 handleSubmit 根本不会被调用——不给个 toast 的话，用户点了
  // 「确认」会像什么都没发生（错误只在对应字段下方一行小字，弹窗滚动区里很容易看不到）。
  function handleInvalid(formErrors: Record<string, { message?: string } | undefined>) {
    const first = Object.entries(formErrors).find(([, e]) => e?.message);
    const fieldLabel = first
      ? (fields.find((f) => f.name === first[0])?.label ?? first[0])
      : undefined;
    showToast({
      variant: "error",
      title: "表单校验未通过",
      description: first?.[1]?.message
        ? fieldLabel
          ? `${fieldLabel}：${first[1]!.message}`
          : first[1]!.message
        : "请检查各字段填写是否正确。",
    });
  }

  async function confirmDelete() {
    if (!deleting) return;
    try {
      await onDelete!(rowId(deleting));
      onChanged?.();
      setDeleting(null);
    } catch (err) {
      const msg =
        err instanceof ApiError ? (err.problem?.title ?? `删除失败（${err.status}）`) : "删除失败";
      showToast({ variant: "error", title: "无法删除", description: msg });
      throw err; // 让 ConfirmDialog 保持打开
    }
  }

  const createDialog = (
    <Dialog open={mode !== "closed"} onOpenChange={(next) => (next ? null : closeDialog())}>
      <DialogContent className="flex max-h-[85vh] max-w-lg flex-col overflow-hidden">
        <DialogHeader className="shrink-0">
          <DialogTitle>
            {mode !== "closed" && mode !== "create" ? editDialogTitle : dialogTitle}
          </DialogTitle>
        </DialogHeader>
        <form
          className="flex min-h-0 flex-1 flex-col gap-4"
          onSubmit={form.handleSubmit(handleSubmit, (formErrors) =>
            handleInvalid(formErrors as Record<string, { message?: string } | undefined>),
          )}
        >
          <div className="min-h-0 flex-1 space-y-4 overflow-y-auto pr-1">
            {fields.map((field) => {
              const locked =
                mode !== "closed" && mode !== "create" && !!lockOnEdit?.includes(field.name);
              return (
                <div key={field.name} className="space-y-2">
                  {field.kind !== "switch" ? (
                    <Label htmlFor={field.name}>{field.label}</Label>
                  ) : null}
                  <CrudFieldControl field={field} form={form} disabled={locked} />
                  {locked ? (
                    <p className="text-xs text-muted-foreground">
                      此字段不可编辑；如需更改请新建模板并停用旧行。
                    </p>
                  ) : field.description ? (
                    <p className="text-xs text-muted-foreground">{field.description}</p>
                  ) : null}
                  {errors[field.name]?.message ? (
                    <p className="text-xs text-destructive">{errors[field.name]!.message}</p>
                  ) : null}
                </div>
              );
            })}
            {submitError ? <ErrorBanner error={submitError} /> : null}
          </div>
          <DialogFooter className="shrink-0">
            <Button type="submit" disabled={form.formState.isSubmitting}>
              {form.formState.isSubmitting ? "提交中…" : "确认"}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );

  if (dialogOnly) return createDialog;

  return (
    <div className="space-y-4">
      {title ? (
        <div className="flex items-center justify-between gap-4">
          {title}
          {!hideCreate ? (
            <Button onClick={openCreate}>
              <PlusCircle className="size-4" />
              {createButtonLabel}
            </Button>
          ) : null}
        </div>
      ) : !hideCreate ? (
        <div className="flex justify-end">
          <Button onClick={openCreate}>
            <PlusCircle className="size-4" />
            {createButtonLabel}
          </Button>
        </div>
      ) : null}

      {createDialog}

      {isLoading ? (
        <Skeleton className="h-48 w-full rounded-xl" />
      ) : loadError ? (
        <ErrorBanner error={loadError} />
      ) : items?.length ? (
        <div className="overflow-x-auto rounded-xl border border-border">
          <Table>
            <TableHeader>
              <TableRow>
                {cols.map((c) => (
                  <TableHead key={c.key}>{c.header}</TableHead>
                ))}
                {actionsColumn ? <TableHead className="w-24 text-right">操作</TableHead> : null}
                {renderExpanded ? <TableHead className="w-10" /> : null}
              </TableRow>
            </TableHeader>
            <TableBody>
              {items.map((item) => {
                const id = rowId(item);
                const expanded = expandedId === id;
                const colCount = cols.length + (renderExpanded ? 1 : 0) + (actionsColumn ? 1 : 0);
                return (
                  <Fragment key={id}>
                    <TableRow>
                      {cols.map((c) => (
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
                      {renderExpanded ? (
                        <TableCell className="w-10 text-right align-middle">
                          <button
                            type="button"
                            onClick={() => setExpandedId(expanded ? null : id)}
                            className="-m-2 inline-flex items-center justify-center p-2 text-muted-foreground transition-colors hover:text-foreground"
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
  disabled = false,
}: {
  field: CrudField<TFormValues>;
  form: UseFormReturn<TFormValues>;
  disabled?: boolean;
}) {
  if (field.kind === "text" || field.kind === "date") {
    return (
      <Input
        id={field.name}
        type={field.kind === "date" ? "date" : "text"}
        disabled={disabled}
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
        disabled={disabled}
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
        disabled={disabled}
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
            <Switch
              id={field.name}
              disabled={disabled}
              checked={Boolean(value)}
              onCheckedChange={onChange}
            />
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
          disabled={disabled}
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
