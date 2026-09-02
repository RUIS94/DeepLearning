"use client";

import { createContext, useCallback, useContext, useState, type ReactNode } from "react";
import { useRouter } from "next/navigation";
import { useMutation } from "@tanstack/react-query";
import { Controller, useFieldArray, useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { Eraser, PlusCircle, Send, Trash2 } from "lucide-react";
import {
  SidePanel,
  SidePanelBody,
  SidePanelContent,
  SidePanelFooter,
  SidePanelHeader,
} from "@/components/ui/side-panel";
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
import { showToast } from "@/components/ui/toast";
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

/** 后端 brief（jsonb）的四个可选子字段；`key` 是拼进 JSON 时用的键名，与答题页 parseBrief 对齐。 */
const BRIEF_FIELDS = [
  { name: "domain", key: "领域", label: "领域", placeholder: "公共卫生" },
  { name: "textType", key: "文本类型", label: "文本类型", placeholder: "通知" },
  { name: "purpose", key: "目的", label: "目的", placeholder: "告知公众疫苗接种安排" },
  { name: "audience", key: "受众", label: "受众", placeholder: "社区居民" },
] as const;

/** 把非空子字段拼成后端 brief 的 JSON 字符串；全空则回 null。 */
function buildBrief(brief: ImportUserQuestionFormInput["brief"]): string | null {
  const obj: Record<string, string> = {};
  for (const f of BRIEF_FIELDS) {
    const v = brief?.[f.name]?.trim();
    if (v) obj[f.key] = v;
  }
  return Object.keys(obj).length ? JSON.stringify(obj) : null;
}

const defaultValues: ImportUserQuestionFormInput = {
  taskType: TaskType.A,
  difficulty: 1,
  title: "",
  brief: { domain: "", textType: "", purpose: "", audience: "" },
  sourceText: "",
  isSeedReference: false,
  visibility: Visibility.Private,
  meaningCheckpoints: [],
  taskB: { flawedTranslationText: "", seededErrors: [] },
};

const ImportPanelContext = createContext<{ open: () => void } | null>(null);

/** 侧栏「导入题目」项调用它来打开面板。 */
export function useImportPanel() {
  const ctx = useContext(ImportPanelContext);
  if (!ctx) throw new Error("useImportPanel must be used within <ImportPanelProvider>");
  return ctx;
}

/**
 * 挂在 (app)/layout.tsx —— 常驻不卸载,所以编辑中关掉面板草稿仍在(RHF state 不丢)。
 * 成功导入 或 点「清空」才 reset。「导入题目」按钮成功后锁定(submitted),下次打开面板才解锁,
 * 避免同一份数据被重复插入。
 */
export function ImportPanelProvider({ children }: { children: ReactNode }) {
  const router = useRouter();
  const [open, setOpen] = useState(false);
  const [submitted, setSubmitted] = useState(false);
  const [draft, setDraft] = useState<{ start: number; end: number } | null>(null);
  const [draftTaxonomyId, setDraftTaxonomyId] = useState("");
  const [draftCorrected, setDraftCorrected] = useState("");

  const examType = useExamType();
  const errorTaxonomies = useErrorTaxonomies(examType.data?.id);
  const selectedDraftTaxonomyId = draftTaxonomyId || errorTaxonomies.data?.[0]?.id || "";

  const form = useForm<ImportUserQuestionFormInput>({
    resolver: zodResolver(importUserQuestionSchema),
    defaultValues,
  });
  const taskType = form.watch("taskType");
  const flawedText = form.watch("taskB.flawedTranslationText") ?? "";
  const checkpoints = useFieldArray({ control: form.control, name: "meaningCheckpoints" });
  const seededErrors = useFieldArray({ control: form.control, name: "taskB.seededErrors" });

  const clearAll = useCallback(() => {
    form.reset(defaultValues);
    setDraft(null);
    setDraftTaxonomyId("");
    setDraftCorrected("");
  }, [form]);

  const submit = useMutation({
    mutationFn: (values: ImportUserQuestionFormInput) =>
      importUserQuestion({
        taskType: values.taskType,
        difficulty: values.difficulty,
        title: values.title,
        brief: buildBrief(values.brief),
        sourceText: values.sourceText,
        wordCount: values.wordCount ?? null,
        isSeedReference: values.isSeedReference ?? false,
        visibility: values.visibility ?? Visibility.Private,
        meaningCheckpoints: (values.meaningCheckpoints ?? []).map((c) => ({
          checkpointText: c.checkpointText,
          checkpointType: c.checkpointType ?? null,
          importance: c.importance,
        })),
        flawedTranslationText: values.taskB?.flawedTranslationText ?? null,
        seededErrors: (values.taskB?.seededErrors ?? []).map((e) => ({
          positionStart: e.positionStart,
          positionEnd: e.positionEnd,
          errorTaxonomyId: e.errorTaxonomyId,
          correctReferenceText: e.correctReferenceText,
          note: e.note ?? null,
        })),
      }),
    onSuccess: (question) => {
      setSubmitted(true);
      showToast({ variant: "success", title: "题目已导入", description: "正在进入答题页…" });
      clearAll();
      setOpen(false);
      router.push(`/practice/${question.id}`);
    },
  });

  const openPanel = useCallback(() => {
    if (!submit.isPending) setSubmitted(false);
    setOpen(true);
  }, [submit.isPending]);

  function switchTaskType(next: number) {
    form.setValue("taskType", next);
    form.setValue("taskB", { flawedTranslationText: "", seededErrors: [] });
  }

  return (
    <ImportPanelContext.Provider value={{ open: openPanel }}>
      {children}

      <SidePanel open={open} onOpenChange={setOpen}>
        <SidePanelContent width="38rem">
          <SidePanelHeader
            title="导入题目"
            description="手工录入题目或真题种子。TaskB 需要含错译文并至少标注一条错误。"
          />

          <SidePanelBody>
            <form
              id="import-question-form"
              className="space-y-6"
              onSubmit={form.handleSubmit((values) => submit.mutate(values))}
            >
              <Card className="shadow-none">
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
                      <p className="text-xs text-destructive">
                        {form.formState.errors.title.message}
                      </p>
                    ) : null}
                  </div>

                  <div className="space-y-2">
                    <Label>简介（可选）</Label>
                    <div className="grid gap-4 sm:grid-cols-2">
                      {BRIEF_FIELDS.map((f) => (
                        <div key={f.name} className="space-y-1.5">
                          <Label className="text-xs font-normal text-muted-foreground">
                            {f.label}
                          </Label>
                          <Input
                            {...form.register(`brief.${f.name}` as const)}
                            placeholder={f.placeholder}
                          />
                          {form.formState.errors.brief?.[f.name] ? (
                            <p className="text-xs text-destructive">
                              {form.formState.errors.brief[f.name]?.message}
                            </p>
                          ) : null}
                        </div>
                      ))}
                    </div>
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
                        供 AI 出题时作为 few-shot 参考样本检索。
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
                      checkpoints.append({
                        checkpointText: "",
                        checkpointType: null,
                        importance: 0,
                      })
                    }
                  >
                    <PlusCircle className="size-4" />
                    添加
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

              {submit.isError ? <ErrorBanner error={submit.error} /> : null}
            </form>
          </SidePanelBody>

          <SidePanelFooter>
            <Button
              type="button"
              variant="ghost"
              size="sm"
              className="mr-auto text-muted-foreground"
              disabled={submit.isPending}
              onClick={clearAll}
            >
              <Eraser className="size-4" />
              清空
            </Button>
            <Button type="button" variant="outline" size="sm" onClick={() => setOpen(false)}>
              取消
            </Button>
            <Button
              type="submit"
              form="import-question-form"
              size="sm"
              disabled={submit.isPending || submitted}
            >
              <Send className="size-4" />
              {submit.isPending ? "导入中…" : "导入题目"}
            </Button>
          </SidePanelFooter>
        </SidePanelContent>
      </SidePanel>
    </ImportPanelContext.Provider>
  );
}
