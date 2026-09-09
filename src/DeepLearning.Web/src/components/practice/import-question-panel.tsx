"use client";

import { createContext, useCallback, useContext, useState, type ReactNode } from "react";
import { useRouter } from "next/navigation";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
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
import { listCategories, tagQuestionWithCategory } from "@/lib/api/exam-config";
import { useErrorTaxonomies, useExamType } from "@/hooks/use-exam-config";
import { TaskType, Visibility } from "@/lib/types/enums";
import {
  importUserQuestionSchema,
  type ImportUserQuestionFormInput,
} from "@/lib/validation/question-import";
import { tFormError, useT } from "@/lib/i18n";
import { useEnumLabels } from "@/lib/i18n/enum-labels";
import type { MessageKey } from "@/lib/i18n/messages/en";

/** 后端 brief（jsonb）的四个可选子字段；`key` 是拼进 JSON 时用的键名，与答题页 parseBrief 对齐，不可改。 */
const BRIEF_FIELDS = [
  {
    name: "domain",
    key: "领域",
    labelKey: "answer.brief.domain",
    placeholderKey: "import.brief.domainPh",
  },
  {
    name: "textType",
    key: "文本类型",
    labelKey: "answer.brief.textType",
    placeholderKey: "import.brief.textTypePh",
  },
  {
    name: "purpose",
    key: "目的",
    labelKey: "answer.brief.purpose",
    placeholderKey: "import.brief.purposePh",
  },
  {
    name: "audience",
    key: "受众",
    labelKey: "answer.brief.audience",
    placeholderKey: "import.brief.audiencePh",
  },
] as const satisfies readonly {
  name: string;
  key: string;
  labelKey: MessageKey;
  placeholderKey: MessageKey;
}[];

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
  const t = useT();
  const { CheckpointImportanceLabel, DifficultyLabel, TaskTypeLabel } = useEnumLabels();
  const router = useRouter();
  const queryClient = useQueryClient();
  const [open, setOpen] = useState(false);
  const [submitted, setSubmitted] = useState(false);
  const [draft, setDraft] = useState<{ start: number; end: number } | null>(null);
  const [draftTaxonomyId, setDraftTaxonomyId] = useState("");
  const [draftCorrected, setDraftCorrected] = useState("");
  const [categoryIds, setCategoryIds] = useState<string[]>([]);

  const examType = useExamType();
  const errorTaxonomies = useErrorTaxonomies(examType.data?.id);
  const categories = useQuery({ queryKey: ["categories"], queryFn: () => listCategories() });
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
    setCategoryIds([]);
  }, [form]);

  const submit = useMutation({
    mutationFn: async (values: ImportUserQuestionFormInput) => {
      const question = await importUserQuestion({
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
      });
      // 导入接口不收分类,成功后逐个补打标签,题库的分类筛选器才能筛到手工导入的题。
      for (const categoryId of categoryIds) {
        await tagQuestionWithCategory(categoryId, question.id);
      }
      return question;
    },
    onSuccess: (question) => {
      setSubmitted(true);
      showToast({
        variant: "success",
        title: t("import.imported.title"),
        description: t("practice.generated.description"),
      });
      queryClient.invalidateQueries({ queryKey: ["questions"] });
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
            title={t("practice.importQuestion")}
            description={t("import.description")}
          />

          <SidePanelBody>
            <form
              id="import-question-form"
              className="space-y-6"
              onSubmit={form.handleSubmit((values) => submit.mutate(values))}
            >
              <Card className="shadow-none">
                <CardHeader>
                  <CardTitle className="text-base">{t("import.basicInfo")}</CardTitle>
                </CardHeader>
                <CardContent className="space-y-4">
                  <div className="grid gap-4 sm:grid-cols-2">
                    <div className="space-y-2">
                      <Label>{t("practice.filter.taskType")}</Label>
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
                      <Label>{t("practice.filter.difficulty")}</Label>
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
                    <Label>{t("import.titleLabel")}</Label>
                    <Input {...form.register("title")} placeholder={t("import.titlePh")} />
                    {form.formState.errors.title ? (
                      <p className="text-xs text-destructive">
                        {tFormError(t, form.formState.errors.title.message)}
                      </p>
                    ) : null}
                  </div>

                  <div className="space-y-2">
                    <Label>{t("import.briefLabel")}</Label>
                    <div className="grid gap-4 sm:grid-cols-2">
                      {BRIEF_FIELDS.map((f) => (
                        <div key={f.name} className="space-y-1.5">
                          <Label className="text-xs font-normal text-muted-foreground">
                            {t(f.labelKey)}
                          </Label>
                          <Input
                            {...form.register(`brief.${f.name}` as const)}
                            placeholder={t(f.placeholderKey)}
                          />
                          {form.formState.errors.brief?.[f.name] ? (
                            <p className="text-xs text-destructive">
                              {tFormError(t, form.formState.errors.brief[f.name]?.message)}
                            </p>
                          ) : null}
                        </div>
                      ))}
                    </div>
                  </div>

                  <div className="space-y-2">
                    <Label>{t("answer.sourceText")}</Label>
                    <Textarea rows={6} className="source-text" {...form.register("sourceText")} />
                    {form.formState.errors.sourceText ? (
                      <p className="text-xs text-destructive">
                        {tFormError(t, form.formState.errors.sourceText.message)}
                      </p>
                    ) : null}
                  </div>

                  <div className="space-y-2">
                    <Label>{t("import.categoriesLabel")}</Label>
                    <p className="text-xs text-muted-foreground">{t("import.categoriesHint")}</p>
                    {categories.data?.length ? (
                      <div className="flex flex-wrap gap-2 pt-1">
                        {categories.data.map((c) => {
                          const active = categoryIds.includes(c.id);
                          return (
                            <button
                              key={c.id}
                              type="button"
                              onClick={() =>
                                setCategoryIds((prev) =>
                                  active ? prev.filter((id) => id !== c.id) : [...prev, c.id],
                                )
                              }
                            >
                              <Badge
                                variant={active ? "default" : "outline"}
                                className={active ? "" : "border-border text-muted-foreground"}
                              >
                                {c.name}
                              </Badge>
                            </button>
                          );
                        })}
                      </div>
                    ) : (
                      <p className="text-xs text-muted-foreground">{t("import.noCategories")}</p>
                    )}
                  </div>

                  <div className="flex items-center justify-between rounded-lg border border-border p-3">
                    <div>
                      <p className="text-sm font-medium">{t("import.markSeed")}</p>
                      <p className="text-xs text-muted-foreground">{t("import.markSeedHint")}</p>
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
                  <CardTitle className="text-base">{t("import.meaningPoints")}</CardTitle>
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
                    {t("common.add")}
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
                          placeholder={t("import.meaningPointPh")}
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
                    <p className="text-sm text-muted-foreground">{t("import.noMeaningPoints")}</p>
                  ) : null}
                </CardContent>
              </Card>

              {taskType === TaskType.B ? (
                <Card className="border-border shadow-none">
                  <CardHeader>
                    <CardTitle className="text-base">{t("import.taskBCard")}</CardTitle>
                  </CardHeader>
                  <CardContent className="space-y-4">
                    <div className="space-y-2">
                      <Label>{t("import.flawedFullText")}</Label>
                      <Textarea
                        rows={5}
                        className="source-text"
                        {...form.register("taskB.flawedTranslationText" as const)}
                      />
                    </div>

                    {flawedText ? (
                      <div className="space-y-3">
                        <Label>{t("import.dragToAnnotate")}</Label>
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
                          {t("import.selection", { start: draft.start, end: draft.end })}
                        </p>
                        <Select value={selectedDraftTaxonomyId} onValueChange={setDraftTaxonomyId}>
                          <SelectTrigger>
                            <SelectValue />
                          </SelectTrigger>
                          <SelectContent>
                            {(errorTaxonomies.data ?? []).map((tax) => (
                              <SelectItem key={tax.id} value={tax.id}>
                                {tax.categoryName}
                              </SelectItem>
                            ))}
                          </SelectContent>
                        </Select>
                        <Input
                          value={draftCorrected}
                          onChange={(e) => setDraftCorrected(e.target.value)}
                          placeholder={t("import.correctTranslationPh")}
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
                            {t("import.addSeededError")}
                          </Button>
                          <Button
                            type="button"
                            size="sm"
                            variant="ghost"
                            onClick={() => setDraft(null)}
                          >
                            {t("common.cancel")}
                          </Button>
                        </div>
                      </div>
                    ) : null}

                    <div className="space-y-2">
                      <p className="text-numeric text-xs font-medium text-muted-foreground">
                        {t("answer.annotatedCount", { count: seededErrors.fields.length })}
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
                                  errorTaxonomies.data?.find((tax) => tax.id === f.errorTaxonomyId)
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
                        {tFormError(
                          t,
                          (form.formState.errors.taskB as { message?: string }).message,
                        ) ?? t("import.taskBError")}
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
              {t("common.clear")}
            </Button>
            <Button type="button" variant="outline" size="sm" onClick={() => setOpen(false)}>
              {t("common.cancel")}
            </Button>
            <Button
              type="submit"
              form="import-question-form"
              size="sm"
              disabled={submit.isPending || submitted}
            >
              <Send className="size-4" />
              {submit.isPending ? t("import.importing") : t("practice.importQuestion")}
            </Button>
          </SidePanelFooter>
        </SidePanelContent>
      </SidePanel>
    </ImportPanelContext.Provider>
  );
}
