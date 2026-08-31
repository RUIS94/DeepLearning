"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { useMutation } from "@tanstack/react-query";
import { Controller, useFieldArray, useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { PlusCircle, Send, Trash2 } from "lucide-react";
import { AdminShell } from "@/components/shared/admin-shell";
import { ErrorBanner } from "@/components/shared/ai-loading-state";
import { SelectableSourceText } from "@/components/practice/selectable-source-text";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { Label } from "@/components/ui/label";
import { Switch } from "@/components/ui/switch";
import { Badge } from "@/components/ui/badge";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { importUserQuestion } from "@/lib/api/questions";
import { useErrorTaxonomies, useExamType } from "@/hooks/use-exam-config";
import {
  CheckpointImportanceLabel,
  DifficultyLabel,
  TaskType,
  TaskTypeLabel,
  Visibility,
} from "@/lib/types/enums";
import {
  importUserQuestionSchema,
  type ImportUserQuestionFormInput,
} from "@/lib/validation/question-import";

const defaultValues: ImportUserQuestionFormInput = {
  taskType: TaskType.A,
  difficulty: 1,
  title: "",
  brief: "",
  sourceText: "",
  isSeedReference: false,
  visibility: Visibility.Private,
  meaningCheckpoints: [],
  taskB: null,
};

export function ImportQuestionPage() {
  const router = useRouter();
  const examType = useExamType();
  const errorTaxonomies = useErrorTaxonomies(examType.data?.id);
  const [draft, setDraft] = useState<{ start: number; end: number } | null>(null);
  // 以前是 useState(errorTaxonomies[0]!.id) 同步初始化——现在要异步查询才能拿到，
  // 初始为空字符串，渲染 Select 时 fallback 到已加载数据的第一项（同 answer-page.tsx 的处理）。
  const [draftTaxonomyId, setDraftTaxonomyId] = useState("");
  const [draftCorrected, setDraftCorrected] = useState("");
  const selectedDraftTaxonomyId = draftTaxonomyId || errorTaxonomies.data?.[0]?.id || "";

  const form = useForm<ImportUserQuestionFormInput>({
    resolver: zodResolver(importUserQuestionSchema),
    defaultValues,
  });
  const taskType = form.watch("taskType");
  const flawedText = form.watch("taskB.flawedTranslationText") ?? "";

  const checkpoints = useFieldArray({ control: form.control, name: "meaningCheckpoints" });
  const seededErrors = useFieldArray({ control: form.control, name: "taskB.seededErrors" });

  const submit = useMutation({
    mutationFn: (values: ImportUserQuestionFormInput) =>
      importUserQuestion({
        taskType: values.taskType,
        difficulty: values.difficulty,
        title: values.title,
        brief: values.brief ?? null,
        sourceText: values.sourceText,
        wordCount: values.wordCount ?? null,
        isSeedReference: values.isSeedReference ?? false,
        visibility: values.visibility ?? Visibility.Private,
        meaningCheckpoints: (values.meaningCheckpoints ?? []).map((c) => ({
          checkpointText: c.checkpointText,
          checkpointType: c.checkpointType ?? null,
          importance: c.importance,
        })),
        // 镜像后端 ImportUserQuestionCommand：flawedTranslationText/seededErrors 是顶层平铺
        // 字段，不是嵌套在 taskB 里——表单内部仍用 taskB 分组管理这些字段更符合 UI 习惯，
        // 只在构造请求体这一步展开。
        flawedTranslationText: values.taskB?.flawedTranslationText ?? null,
        seededErrors:
          values.taskB?.seededErrors.map((e) => ({
            positionStart: e.positionStart,
            positionEnd: e.positionEnd,
            errorTaxonomyId: e.errorTaxonomyId,
            correctReferenceText: e.correctReferenceText,
            note: e.note ?? null,
          })) ?? [],
      }),
    onSuccess: (question) => router.push(`/practice/${question.id}`),
  });

  function switchTaskType(next: number) {
    form.setValue("taskType", next);
    if (next === TaskType.B && !form.getValues("taskB")) {
      form.setValue("taskB", { flawedTranslationText: "", seededErrors: [] });
    }
    if (next === TaskType.A) {
      form.setValue("taskB", null);
    }
  }

  return (
    <AdminShell
      title="导入题目"
      description="手工录入题目或真题种子。TaskB 需要含错译文并至少标注一条种子错误（校验规则见 ImportUserQuestionValidator）。"
    >
      <form
        className="grid gap-6 lg:grid-cols-[1fr_380px]"
        onSubmit={form.handleSubmit((values) => submit.mutate(values))}
      >
        <div className="space-y-6">
          <Card className="border-border shadow-none">
            <CardHeader>
              <CardTitle className="text-base">基本信息</CardTitle>
            </CardHeader>
            <CardContent className="space-y-4">
              <div className="grid gap-4 sm:grid-cols-2">
                <div className="space-y-2">
                  <Label>任务类型</Label>
                  <Controller
                    control={form.control}
                    name="taskType"
                    render={({ field }) => (
                      <Select
                        value={String(field.value)}
                        onValueChange={(v) => switchTaskType(Number(v))}
                      >
                        <SelectTrigger>
                          <SelectValue />
                        </SelectTrigger>
                        <SelectContent>
                          {Object.entries(TaskTypeLabel).map(([v, l]) => (
                            <SelectItem key={v} value={v}>
                              {l}
                            </SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                    )}
                  />
                </div>
                <div className="space-y-2">
                  <Label>难度</Label>
                  <Controller
                    control={form.control}
                    name="difficulty"
                    render={({ field }) => (
                      <Select
                        value={String(field.value)}
                        onValueChange={(v) => field.onChange(Number(v))}
                      >
                        <SelectTrigger>
                          <SelectValue />
                        </SelectTrigger>
                        <SelectContent>
                          {Object.entries(DifficultyLabel).map(([v, l]) => (
                            <SelectItem key={v} value={v}>
                              {l}
                            </SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                    )}
                  />
                </div>
              </div>

              <div className="space-y-2">
                <Label>标题</Label>
                <Input {...form.register("title")} placeholder="社区健康中心疫苗接种通知" />
                {form.formState.errors.title ? (
                  <p className="text-xs text-destructive">{form.formState.errors.title.message}</p>
                ) : null}
              </div>

              <div className="space-y-2">
                <Label>简介（可选）</Label>
                <Input {...form.register("brief")} placeholder="公共卫生类通知，注意语域" />
              </div>

              <div className="space-y-2">
                <Label>原文</Label>
                <Textarea rows={6} className="source-text" {...form.register("sourceText")} />
                {form.formState.errors.sourceText ? (
                  <p className="text-xs text-destructive">
                    {form.formState.errors.sourceText.message}
                  </p>
                ) : null}
              </div>

              <div className="flex items-center justify-between rounded-lg border border-border p-3">
                <div>
                  <p className="text-sm font-medium">标记为真题种子</p>
                  <p className="text-xs text-muted-foreground">
                    供 AI 出题时作为 few-shot 参考样本检索（isSeedReference）。
                  </p>
                </div>
                <Controller
                  control={form.control}
                  name="isSeedReference"
                  render={({ field }) => (
                    <Switch checked={Boolean(field.value)} onCheckedChange={field.onChange} />
                  )}
                />
              </div>
            </CardContent>
          </Card>

          <Card className="border-border shadow-none">
            <CardHeader className="flex-row items-center justify-between space-y-0">
              <CardTitle className="text-base">核心意义点（可选）</CardTitle>
              <Button
                type="button"
                size="sm"
                variant="outline"
                onClick={() =>
                  checkpoints.append({ checkpointText: "", checkpointType: null, importance: 0 })
                }
              >
                <PlusCircle className="size-4" />
                添加意义点
              </Button>
            </CardHeader>
            <CardContent className="space-y-3">
              {checkpoints.fields.map((f, i) => (
                <div
                  key={f.id}
                  className="flex items-start gap-2 rounded-md border border-border p-3"
                >
                  <div className="flex-1 space-y-2">
                    <Input
                      placeholder="该信息点的具体内容"
                      {...form.register(`meaningCheckpoints.${i}.checkpointText` as const)}
                    />
                    <Controller
                      control={form.control}
                      name={`meaningCheckpoints.${i}.importance` as const}
                      render={({ field }) => (
                        <Select
                          value={String(field.value)}
                          onValueChange={(v) => field.onChange(Number(v))}
                        >
                          <SelectTrigger className="w-32">
                            <SelectValue />
                          </SelectTrigger>
                          <SelectContent>
                            {Object.entries(CheckpointImportanceLabel).map(([v, l]) => (
                              <SelectItem key={v} value={v}>
                                {l}
                              </SelectItem>
                            ))}
                          </SelectContent>
                        </Select>
                      )}
                    />
                  </div>
                  <Button
                    type="button"
                    size="icon"
                    variant="ghost"
                    onClick={() => checkpoints.remove(i)}
                  >
                    <Trash2 className="size-4" />
                  </Button>
                </div>
              ))}
              {checkpoints.fields.length === 0 ? (
                <p className="text-sm text-muted-foreground">尚未添加意义点。</p>
              ) : null}
            </CardContent>
          </Card>

          {taskType === TaskType.B ? (
            <Card className="border-border shadow-none">
              <CardHeader>
                <CardTitle className="text-base">TaskB：含错译文与种子错误</CardTitle>
              </CardHeader>
              <CardContent className="space-y-4">
                <div className="space-y-2">
                  <Label>含错译文全文</Label>
                  <Textarea
                    rows={5}
                    className="source-text"
                    {...form.register("taskB.flawedTranslationText" as const)}
                  />
                </div>

                {flawedText ? (
                  <div className="space-y-3">
                    <Label>拖选标注种子错误</Label>
                    <SelectableSourceText
                      text={flawedText}
                      highlightRanges={seededErrors.fields.map((f) => ({
                        positionStart: f.positionStart,
                        positionEnd: f.positionEnd,
                        tone: "seed" as const,
                      }))}
                      onSelectRange={(start, end) => {
                        setDraft({ start, end });
                        setDraftCorrected(flawedText.slice(start, end));
                      }}
                    />
                  </div>
                ) : null}

                {draft ? (
                  <div className="space-y-3 rounded-lg border border-primary/40 bg-primary/5 p-4">
                    <p className="text-numeric text-xs text-muted-foreground">
                      选区 [{draft.start}, {draft.end})
                    </p>
                    <Select value={selectedDraftTaxonomyId} onValueChange={setDraftTaxonomyId}>
                      <SelectTrigger>
                        <SelectValue />
                      </SelectTrigger>
                      <SelectContent>
                        {(errorTaxonomies.data ?? []).map((t) => (
                          <SelectItem key={t.id} value={t.id}>
                            {t.categoryName}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                    <Input
                      value={draftCorrected}
                      onChange={(e) => setDraftCorrected(e.target.value)}
                      placeholder="正确译法"
                    />
                    <div className="flex gap-2">
                      <Button
                        type="button"
                        size="sm"
                        disabled={!selectedDraftTaxonomyId}
                        onClick={() => {
                          seededErrors.append({
                            positionStart: draft.start,
                            positionEnd: draft.end,
                            errorTaxonomyId: selectedDraftTaxonomyId,
                            correctReferenceText: draftCorrected,
                          });
                          setDraft(null);
                        }}
                      >
                        添加种子错误
                      </Button>
                      <Button
                        type="button"
                        size="sm"
                        variant="ghost"
                        onClick={() => setDraft(null)}
                      >
                        取消
                      </Button>
                    </div>
                  </div>
                ) : null}

                <div className="space-y-2">
                  <p className="text-numeric text-xs font-medium text-muted-foreground">
                    已标注 {seededErrors.fields.length} 处
                  </p>
                  {seededErrors.fields.map((f, i) => (
                    <div
                      key={f.id}
                      className="flex items-start justify-between gap-3 rounded-md border border-border p-3"
                    >
                      <div className="space-y-1 text-sm">
                        <div className="flex items-center gap-2">
                          <Badge variant="outline" className="border-accent/40 text-accent">
                            {
                              errorTaxonomies.data?.find((t) => t.id === f.errorTaxonomyId)
                                ?.categoryName
                            }
                          </Badge>
                          <span className="text-numeric text-xs text-muted-foreground">
                            [{f.positionStart}, {f.positionEnd})
                          </span>
                        </div>
                        <p>
                          <span className="line-through opacity-60">
                            {flawedText.slice(f.positionStart, f.positionEnd)}
                          </span>
                          <span className="mx-1">→</span>
                          <span className="text-primary">{f.correctReferenceText}</span>
                        </p>
                      </div>
                      <Button
                        type="button"
                        size="icon"
                        variant="ghost"
                        onClick={() => seededErrors.remove(i)}
                      >
                        <Trash2 className="size-4" />
                      </Button>
                    </div>
                  ))}
                </div>
                {form.formState.errors.taskB ? (
                  <p className="text-xs text-destructive">
                    {(form.formState.errors.taskB as { message?: string }).message ??
                      "请检查种子错误的区间与分类填写是否完整"}
                  </p>
                ) : null}
              </CardContent>
            </Card>
          ) : null}
        </div>

        <div className="space-y-6">
          <Card className="h-fit border-border shadow-none">
            <CardHeader>
              <CardTitle className="text-base">提交</CardTitle>
            </CardHeader>
            <CardContent className="space-y-3">
              <Button type="submit" className="w-full" disabled={submit.isPending}>
                <Send className="size-4" />
                {submit.isPending ? "导入中…" : "导入题目"}
              </Button>
              {submit.isError ? <ErrorBanner error={submit.error} /> : null}
              <p className="text-xs text-muted-foreground">
                成功后会直接跳转到该题目的答题页，方便立即验证。
              </p>
            </CardContent>
          </Card>
        </div>
      </form>
    </AdminShell>
  );
}
