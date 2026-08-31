"use client";

import { useState, type ReactNode } from "react";
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
import { PlusCircle } from "lucide-react";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from "@/components/ui/dialog";
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
 * 通用后台增改列表（方案 §8.2）。
 * 承载 admin 里"只有 List+Create"的资源页面（exam-types/dimensions/error-taxonomies/
 * prompt-templates/question-bank-categories，见方案 §3.7）——列定义 + 表单 schema 作为 props 传入，
 * 不为每个资源重复写一遍表格+新建弹窗。
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
  dialogTitle = "新建",
  createButtonLabel = "新建",
  emptyMessage = "暂无数据",
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
  dialogTitle?: string;
  createButtonLabel?: string;
  emptyMessage?: string;
}) {
  const [open, setOpen] = useState(false);
  const [submitError, setSubmitError] = useState<unknown>(null);
  const form = useForm<TFormValues>({ resolver: zodResolver(schema), defaultValues });
  const errors = form.formState.errors as Record<string, { message?: string } | undefined>;

  async function handleSubmit(values: TFormValues) {
    setSubmitError(null);
    try {
      const created = await onCreate(values);
      onCreated?.(created);
      setOpen(false);
      form.reset(defaultValues);
    } catch (err) {
      setSubmitError(err);
    }
  }

  return (
    <div className="space-y-4">
      <div className="flex justify-end">
        <Dialog
          open={open}
          onOpenChange={(next) => {
            setOpen(next);
            if (!next) {
              form.reset(defaultValues);
              setSubmitError(null);
            }
          }}
        >
          <DialogTrigger asChild>
            <Button>
              <PlusCircle className="size-4" />
              {createButtonLabel}
            </Button>
          </DialogTrigger>
          <DialogContent className="max-h-[85vh] max-w-lg overflow-y-auto">
            <DialogHeader>
              <DialogTitle>{dialogTitle}</DialogTitle>
            </DialogHeader>
            <form className="space-y-4" onSubmit={form.handleSubmit(handleSubmit)}>
              {fields.map((field) => (
                <div key={field.name} className="space-y-2">
                  {field.kind !== "switch" ? (
                    <Label htmlFor={field.name}>{field.label}</Label>
                  ) : null}
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
                  {form.formState.isSubmitting ? "提交中…" : "确认创建"}
                </Button>
              </DialogFooter>
            </form>
          </DialogContent>
        </Dialog>
      </div>

      {isLoading ? (
        <Skeleton className="h-48 w-full rounded-xl" />
      ) : loadError ? (
        <ErrorBanner error={loadError} />
      ) : items?.length ? (
        <div className="overflow-x-auto rounded-xl border border-border">
          <Table>
            <TableHeader>
              <TableRow>
                {columns.map((c) => (
                  <TableHead key={c.key}>{c.header}</TableHead>
                ))}
              </TableRow>
            </TableHeader>
            <TableBody>
              {items.map((item) => (
                <TableRow key={getRowId(item)}>
                  {columns.map((c) => (
                    <TableCell key={c.key} className={c.className}>
                      {c.render(item)}
                    </TableCell>
                  ))}
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </div>
      ) : (
        <p className="rounded-lg border border-dashed border-border p-10 text-center text-sm text-muted-foreground">
          {emptyMessage}
        </p>
      )}
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
