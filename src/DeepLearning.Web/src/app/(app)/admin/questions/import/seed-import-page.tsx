"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { useMutation } from "@tanstack/react-query";
import { Controller, useFieldArray, useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { PlusCircle, Send, Eraser } from "lucide-react";
import { PageShell } from "@/components/shell/page-shell";
import { ErrorBanner } from "@/components/shared/ai-loading-state";
import { TaskBAnnotator } from "@/components/practice/task-b-annotator";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { Label } from "@/components/ui/label";
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
import { TaskType } from "@/lib/types/enums";
import {
  importUserQuestionSchema,
  type ImportUserQuestionFormInput,
} from "@/lib/validation/question-import";
import { tFormError, useT } from "@/lib/i18n";
import { useEnumLabels } from "@/lib/i18n/enum-labels";
import type { MessageKey } from "@/lib/i18n/messages/en";
import { enumOptions } from "@/lib/enum-options";
import { buildBrief } from "@/lib/brief";

/** 与 import-question-panel.tsx 的 BRIEF_FIELDS 保持一致——两处都要跟 lib/brief.ts 的 BRIEF_FIELD_KEY 对齐。 */
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

const defaultValues: ImportUserQuestionFormInput = {
  taskType: TaskType.A,
  difficulty: 1,
  title: "",
  brief: { domain: "", textType: "", purpose: "", audience: "" },
  sourceText: "",
  meaningCheckpoints: [],
  taskB: { flawedTranslationText: "", seededErrors: [] },
};

/**
 * Admin-only real-exam seed import (D2'/A2', ref/管理员与用户权限隔离_策划书.md) — the only way
 * in this app to produce a Visibility=Shared question. IsSeedReference is always true here (the
 * regular user-facing import-question-panel.tsx never sends it); the backend derives Visibility
 * from it and 403s a non-admin caller outright. This intentionally does not offer question-bank
 * category tagging — that still happens from the exam-management categories tab after import.
 */
export function SeedImportPage() {
  const t = useT();
  const { CheckpointImportanceLabel, DifficultyLabel, TaskTypeLabel } = useEnumLabels();
  const router = useRouter();
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

  function switchTaskType(next: number) {
    form.setValue("taskType", next);
    form.setValue("taskB", { flawedTranslationText: "", seededErrors: [] });
  }

  const submit = useMutation({
    mutationFn: async (values: ImportUserQuestionFormInput) =>
      importUserQuestion({
        taskType: values.taskType,
        difficulty: values.difficulty,
        title: values.title,
        brief: buildBrief(values.brief),
        sourceText: values.sourceText,
        wordCount: values.wordCount ?? null,
        isSeedReference: true,
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
      showToast({
        variant: "success",
        title: t("admin.seedImport.imported"),
        description: t("practice.generated.description"),
      });
      router.push(`/practice/${question.id}`);
    },
  });

  return (
    <PageShell
      title={t("admin.seedImport.title")}
      description={t("admin.seedImport.description")}
      back
      backHref="/admin/users"
    >
      <form
        className="max-w-2xl space-y-6"
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
                        {enumOptions(TaskTypeLabel).map((o) => (
                          <SelectItem key={o.value} value={o.value}>
                            {o.label}
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
                        {enumOptions(DifficultyLabel).map((o) => (
                          <SelectItem key={o.value} value={o.value}>
                            {o.label}
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
                checkpoints.append({ checkpointText: "", checkpointType: null, importance: 0 })
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
                          {enumOptions(CheckpointImportanceLabel).map((o) => (
                            <SelectItem key={o.value} value={o.value}>
                              {o.label}
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
                  <Eraser className="size-4" />
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

              <TaskBAnnotator
                sourceText={flawedText}
                highlightTone="seed"
                showSourceText={flawedText.length > 0}
                dragHintLabel={t("import.dragToAnnotate")}
                items={seededErrors.fields.map((f) => ({
                  key: f.id,
                  start: f.positionStart,
                  end: f.positionEnd,
                  categoryLabel:
                    errorTaxonomies.data?.find((tax) => tax.id === f.errorTaxonomyId)
                      ?.categoryName ?? "",
                  correctedText: f.correctReferenceText,
                }))}
                onRemoveItem={(i) => seededErrors.remove(i)}
                draft={draft}
                onSelectRange={(start, end) => {
                  setDraft({ start, end });
                  setDraftCorrected(flawedText.slice(start, end));
                }}
                onCancelDraft={() => setDraft(null)}
                taxonomyOptions={(errorTaxonomies.data ?? []).map((tax) => ({
                  value: tax.id,
                  label: tax.categoryName,
                }))}
                selectedTaxonomyValue={selectedDraftTaxonomyId}
                onSelectedTaxonomyValueChange={setDraftTaxonomyId}
                correctedText={draftCorrected}
                onCorrectedTextChange={setDraftCorrected}
                correctedTextPlaceholder={t("import.correctTranslationPh")}
                selectionText={
                  draft ? t("import.selection", { start: draft.start, end: draft.end }) : ""
                }
                addButtonText={t("import.addSeededError")}
                addButtonDisabled={!selectedDraftTaxonomyId}
                cancelButtonText={t("common.cancel")}
                onAdd={() => {
                  if (!draft) return;
                  seededErrors.append({
                    positionStart: draft.start,
                    positionEnd: draft.end,
                    errorTaxonomyId: selectedDraftTaxonomyId,
                    correctReferenceText: draftCorrected,
                  });
                  setDraft(null);
                }}
                annotatedCountText={t("answer.annotatedCount", {
                  count: seededErrors.fields.length,
                })}
                buttonType="button"
              />
              {form.formState.errors.taskB ? (
                <p className="text-xs text-destructive">
                  {tFormError(t, (form.formState.errors.taskB as { message?: string }).message) ??
                    t("import.taskBError")}
                </p>
              ) : null}
            </CardContent>
          </Card>
        ) : null}

        {submit.isError ? <ErrorBanner error={submit.error} /> : null}

        <div className="flex justify-end gap-2">
          <Button type="submit" disabled={submit.isPending}>
            <Send className="size-4" />
            {submit.isPending ? t("import.importing") : t("admin.seedImport.submit")}
          </Button>
        </div>
      </form>
    </PageShell>
  );
}
