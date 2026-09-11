"use client";

import { useState, type Ref } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Tag } from "lucide-react";
import {
  CrudTable,
  type CrudColumn,
  type CrudCreateHandle,
  type CrudField,
} from "@/components/admin/crud-table";
import { AiLoadingState } from "@/components/shared/ai-loading-state";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import {
  createQuestionBankCategory,
  deleteQuestionBankCategory,
  listCategories,
  tagQuestionWithCategory,
  updateQuestionBankCategory,
} from "@/lib/api/exam-config";
import { listQuestions } from "@/lib/api/questions";
import {
  questionBankCategoryFormSchema,
  type QuestionBankCategoryFormInput,
} from "@/lib/validation/admin";
import type { QuestionBankCategory } from "@/lib/types/dtos";
import { CategoryType } from "@/lib/types/enums";
import { useT } from "@/lib/i18n";
import { useEnumLabels } from "@/lib/i18n/enum-labels";
import { qk } from "@/lib/query-keys";
import { enumOptions } from "@/lib/enum-options";

const baseDefaultValues: QuestionBankCategoryFormInput = {
  categoryType: 0,
  name: "",
  parentId: "",
  description: "",
  examTypeId: "",
};

function TagQuestionCard({ categories }: { categories: QuestionBankCategory[] }) {
  const t = useT();
  const { CategoryTypeLabel } = useEnumLabels();
  const [categoryId, setCategoryId] = useState("");
  const [questionId, setQuestionId] = useState("");
  const questions = useQuery({
    queryKey: qk.adminQuestionsForTagging(),
    queryFn: () => listQuestions(),
  });
  const tag = useMutation({ mutationFn: () => tagQuestionWithCategory(categoryId, questionId) });

  return (
    <Card className="border-border shadow-none">
      <CardHeader>
        <CardTitle className="text-base">{t("examMgmt.tag.title")}</CardTitle>
      </CardHeader>
      <CardContent className="space-y-4">
        <div className="flex flex-col gap-4 sm:flex-row sm:items-center">
          <Select value={categoryId} onValueChange={setCategoryId}>
            <SelectTrigger className="sm:flex-1">
              <SelectValue placeholder={t("examMgmt.tag.selectCategory")} />
            </SelectTrigger>
            <SelectContent>
              {categories.map((c) => (
                <SelectItem key={c.id} value={c.id}>
                  {CategoryTypeLabel[c.categoryType]} · {c.name}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
          <Select value={questionId} onValueChange={setQuestionId}>
            <SelectTrigger className="sm:flex-1">
              <SelectValue placeholder={t("examMgmt.tag.selectQuestion")} />
            </SelectTrigger>
            <SelectContent>
              {(questions.data ?? []).map((q) => (
                <SelectItem key={q.id} value={q.id}>
                  {q.title}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
          <Button
            className="sm:shrink-0"
            disabled={!categoryId || !questionId || tag.isPending}
            onClick={() => tag.mutate()}
          >
            <Tag className="size-4" />
            {tag.isPending ? t("examMgmt.tag.tagging") : t("examMgmt.tag.tag")}
          </Button>
        </div>
        <AiLoadingState
          status={
            tag.isPending ? "pending" : tag.isSuccess ? "success" : tag.isError ? "error" : "idle"
          }
          error={tag.error}
          pendingHint={t("examMgmt.tag.pendingHint")}
        />
        {tag.isSuccess ? <p className="text-sm text-success">{t("examMgmt.tag.tagged")}</p> : null}
      </CardContent>
    </Card>
  );
}

export function CategoriesPanel({
  examTypeId,
  createRef,
}: {
  examTypeId?: string;
  createRef?: Ref<CrudCreateHandle>;
}) {
  const t = useT();
  const { CategoryTypeLabel } = useEnumLabels();
  const queryClient = useQueryClient();
  const categories = useQuery({
    queryKey: qk.categories(examTypeId ?? null),
    queryFn: () => listCategories(examTypeId),
  });
  // 合并前这里要分别 invalidate ["admin","categories"] 和 ["categories"] 两个命名空间——
  // categories-panel(admin)和 practice-page/import-question-panel(学员端)其实调的是同一个
  // listCategories(examTypeId?) 接口，只是有没有传 examTypeId 的区别，现在共用一个 qk.categories
  // 命名空间，一次 invalidate 就能覆盖所有 examTypeId 变体（代码复用扫描_07_优化计划.md §4.1b）。
  const invalidate = () => queryClient.invalidateQueries({ queryKey: qk.categoriesAll() });

  // 新建/编辑时"作用域"下拉：绑定当前考试类型 or 全局。examTypeId 缺失（理论上不会）时只留全局。
  const scopeOptions = examTypeId
    ? [
        { value: examTypeId, label: t("examMgmt.cat.scopeThisExam") },
        { value: "", label: t("examMgmt.cat.scopeGlobal") },
      ]
    : [{ value: "", label: t("examMgmt.cat.scopeGlobal") }];
  const defaultValues: QuestionBankCategoryFormInput = {
    ...baseDefaultValues,
    examTypeId: examTypeId ?? "",
  };

  const fields: CrudField<QuestionBankCategoryFormInput>[] = [
    {
      name: "categoryType",
      label: t("examMgmt.cat.fieldType"),
      kind: "select",
      valueType: "number",
      options: enumOptions(CategoryTypeLabel),
    },
    {
      name: "name",
      label: t("common.name"),
      kind: "text",
      placeholder: t("examMgmt.cat.namePh"),
    },
    {
      name: "parentId",
      label: t("examMgmt.cat.fieldParent"),
      kind: "select",
      options: [
        { value: "", label: t("examMgmt.cat.parentNone") },
        ...(categories.data ?? []).map((c) => ({ value: c.id, label: c.name })),
      ],
    },
    { name: "description", label: t("examMgmt.cat.fieldDescription"), kind: "textarea" },
    {
      name: "examTypeId",
      label: t("examMgmt.cat.fieldScope"),
      kind: "select",
      options: scopeOptions,
    },
  ];

  const columns: CrudColumn<QuestionBankCategory>[] = [
    { key: "name", header: t("common.name"), render: (c) => c.name },
    {
      key: "parentId",
      header: t("examMgmt.cat.colParent"),
      render: (c) => (categories.data ?? []).find((p) => p.id === c.parentId)?.name ?? "—",
    },
    {
      key: "scope",
      header: t("examMgmt.cat.colScope"),
      render: (c) =>
        c.examTypeId ? t("examMgmt.cat.scopeThisExam") : t("examMgmt.scopeGlobalBadge"),
    },
    { key: "description", header: t("common.description"), render: (c) => c.description ?? "—" },
  ];

  const commonTableProps = {
    hideCreate: true as const,
    columns,
    isLoading: categories.isPending,
    loadError: categories.error,
    getRowId: (c: QuestionBankCategory) => c.id,
    schema: questionBankCategoryFormSchema,
    fields,
    defaultValues,
    dialogTitle: t("examMgmt.cat.dialogTitle"),
    onCreate: (values: QuestionBankCategoryFormInput) =>
      createQuestionBankCategory({
        ...values,
        parentId: values.parentId || null,
        description: values.description || null,
        examTypeId: values.examTypeId || null,
      }),
    toFormValues: (c: QuestionBankCategory) => ({
      categoryType: c.categoryType,
      name: c.name,
      parentId: c.parentId ?? "",
      description: c.description ?? "",
      examTypeId: c.examTypeId ?? "",
    }),
    onUpdate: (id: string, values: QuestionBankCategoryFormInput) =>
      updateQuestionBankCategory(id, {
        name: values.name,
        parentId: values.parentId || null,
        description: values.description || null,
        examTypeId: values.examTypeId || null,
      }),
    onDelete: (id: string) => deleteQuestionBankCategory(id),
    deleteConfirm: (c: QuestionBankCategory) => ({
      title: t("examMgmt.cat.deleteTitle", { name: c.name }),
      description: t("examMgmt.cat.deleteDesc"),
    }),
    onChanged: invalidate,
  };

  const domainItems = categories.data?.filter((c) => c.categoryType === CategoryType.domain);
  const scenarioItems = categories.data?.filter((c) => c.categoryType === CategoryType.scenario);

  return (
    <div className="space-y-6">
      <p className="text-xs text-muted-foreground">{t("examMgmt.sharedNotice")}</p>

      <section className="space-y-2">
        <h3 className="text-sm font-semibold">{t("examMgmt.cat.domainTitle")}</h3>
        <CrudTable openCreateRef={createRef} items={domainItems} {...commonTableProps} />
      </section>

      <section className="space-y-2">
        <h3 className="text-sm font-semibold">{t("examMgmt.cat.scenarioTitle")}</h3>
        <CrudTable items={scenarioItems} {...commonTableProps} />
      </section>

      <TagQuestionCard categories={categories.data ?? []} />
    </div>
  );
}
