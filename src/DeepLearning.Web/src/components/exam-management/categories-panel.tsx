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
import { useT } from "@/lib/i18n";
import { useEnumLabels } from "@/lib/i18n/enum-labels";

const defaultValues: QuestionBankCategoryFormInput = {
  categoryType: 0,
  name: "",
  parentId: "",
  description: "",
};

function TagQuestionCard({ categories }: { categories: QuestionBankCategory[] }) {
  const t = useT();
  const { CategoryTypeLabel } = useEnumLabels();
  const [categoryId, setCategoryId] = useState("");
  const [questionId, setQuestionId] = useState("");
  const questions = useQuery({
    queryKey: ["admin", "questions-for-tagging"],
    queryFn: () => listQuestions(),
  });
  const tag = useMutation({ mutationFn: () => tagQuestionWithCategory(categoryId, questionId) });

  return (
    <Card className="border-border shadow-none">
      <CardHeader>
        <CardTitle className="text-base">{t("examMgmt.tag.title")}</CardTitle>
      </CardHeader>
      <CardContent className="space-y-4">
        <div className="grid gap-4 sm:grid-cols-2">
          <Select value={categoryId} onValueChange={setCategoryId}>
            <SelectTrigger>
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
            <SelectTrigger>
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
        </div>
        <Button disabled={!categoryId || !questionId || tag.isPending} onClick={() => tag.mutate()}>
          <Tag className="size-4" />
          {tag.isPending ? t("examMgmt.tag.tagging") : t("examMgmt.tag.tag")}
        </Button>
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

export function CategoriesPanel({ createRef }: { createRef?: Ref<CrudCreateHandle> }) {
  const t = useT();
  const { CategoryTypeLabel } = useEnumLabels();
  const queryClient = useQueryClient();
  const categories = useQuery({ queryKey: ["admin", "categories"], queryFn: listCategories });
  const invalidate = () => {
    queryClient.invalidateQueries({ queryKey: ["admin", "categories"] });
    queryClient.invalidateQueries({ queryKey: ["categories"] });
  };

  const fields: CrudField<QuestionBankCategoryFormInput>[] = [
    {
      name: "categoryType",
      label: t("examMgmt.cat.fieldType"),
      kind: "select",
      valueType: "number",
      options: Object.entries(CategoryTypeLabel).map(([v, l]) => ({ value: v, label: l })),
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
  ];

  const columns: CrudColumn<QuestionBankCategory>[] = [
    {
      key: "categoryType",
      header: t("examMgmt.cat.colSystem"),
      render: (c) => CategoryTypeLabel[c.categoryType],
    },
    { key: "name", header: t("common.name"), render: (c) => c.name },
    {
      key: "parentId",
      header: t("examMgmt.cat.colParent"),
      render: (c) => (categories.data ?? []).find((p) => p.id === c.parentId)?.name ?? "—",
    },
    { key: "description", header: t("common.description"), render: (c) => c.description ?? "—" },
  ];

  return (
    <div className="space-y-6">
      <CrudTable
        openCreateRef={createRef}
        hideCreate
        columns={columns}
        items={categories.data}
        isLoading={categories.isPending}
        loadError={categories.error}
        getRowId={(c) => c.id}
        schema={questionBankCategoryFormSchema}
        fields={fields}
        defaultValues={defaultValues}
        dialogTitle={t("examMgmt.cat.dialogTitle")}
        onCreate={(values) =>
          createQuestionBankCategory({
            ...values,
            parentId: values.parentId || null,
            description: values.description || null,
          })
        }
        toFormValues={(c) => ({
          categoryType: c.categoryType,
          name: c.name,
          parentId: c.parentId ?? "",
          description: c.description ?? "",
        })}
        onUpdate={(id, values) =>
          updateQuestionBankCategory(id, {
            name: values.name,
            parentId: values.parentId || null,
            description: values.description || null,
          })
        }
        onDelete={(id) => deleteQuestionBankCategory(id)}
        deleteConfirm={(c) => ({
          title: t("examMgmt.cat.deleteTitle", { name: c.name }),
          description: t("examMgmt.cat.deleteDesc"),
        })}
        onChanged={invalidate}
      />

      <TagQuestionCard categories={categories.data ?? []} />
    </div>
  );
}
