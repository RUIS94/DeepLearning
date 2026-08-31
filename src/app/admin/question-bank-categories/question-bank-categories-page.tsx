"use client";

import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Tag } from "lucide-react";
import { AdminShell } from "@/components/shared/admin-shell";
import { CrudTable, type CrudColumn, type CrudField } from "@/components/admin/crud-table";
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
  listCategories,
  listQuestions,
  tagQuestionWithCategory,
} from "@/lib/mock/store";
import { CategoryTypeLabel } from "@/lib/types/enums";
import {
  questionBankCategoryFormSchema,
  type QuestionBankCategoryFormInput,
} from "@/lib/validation/admin";
import type { QuestionBankCategory } from "@/lib/types/dtos";

const defaultValues: QuestionBankCategoryFormInput = {
  categoryType: 0,
  name: "",
  parentId: "",
  description: "",
};

function TagQuestionCard({ categories }: { categories: QuestionBankCategory[] }) {
  const [categoryId, setCategoryId] = useState<string>("");
  const [questionId, setQuestionId] = useState<string>("");

  const questions = useQuery({
    queryKey: ["admin", "questions-for-tagging"],
    queryFn: () => listQuestions(),
  });
  const tag = useMutation({
    mutationFn: () => tagQuestionWithCategory(categoryId, questionId),
  });

  return (
    <Card className="border-border shadow-none">
      <CardHeader>
        <CardTitle className="text-base">给题目打标签</CardTitle>
      </CardHeader>
      <CardContent className="space-y-4">
        <div className="grid gap-4 sm:grid-cols-2">
          <Select value={categoryId} onValueChange={setCategoryId}>
            <SelectTrigger>
              <SelectValue placeholder="选择分类" />
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
              <SelectValue placeholder="选择题目" />
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
          {tag.isPending ? "打标签中…" : "打标签"}
        </Button>
        <AiLoadingState
          status={
            tag.isPending ? "pending" : tag.isSuccess ? "success" : tag.isError ? "error" : "idle"
          }
          error={tag.error}
          pendingHint="正在写入分类映射"
        />
        {tag.isSuccess ? <p className="text-sm text-success">已打标签。</p> : null}
      </CardContent>
    </Card>
  );
}

export function QuestionBankCategoriesPage() {
  const queryClient = useQueryClient();
  const categories = useQuery({ queryKey: ["admin", "categories"], queryFn: listCategories });

  const fields: CrudField<QuestionBankCategoryFormInput>[] = [
    {
      name: "categoryType",
      label: "分类体系",
      kind: "select",
      valueType: "number",
      options: Object.entries(CategoryTypeLabel).map(([v, l]) => ({ value: v, label: l })),
    },
    { name: "name", label: "名称", kind: "text", placeholder: "法律政务 / 移民信件" },
    {
      name: "parentId",
      label: "上级分类（可选，支持层级）",
      kind: "select",
      options: [
        { value: "", label: "无（顶层分类）" },
        ...(categories.data ?? []).map((c) => ({ value: c.id, label: c.name })),
      ],
    },
    { name: "description", label: "描述（可选）", kind: "textarea" },
  ];

  const columns: CrudColumn<QuestionBankCategory>[] = [
    { key: "categoryType", header: "体系", render: (c) => CategoryTypeLabel[c.categoryType] },
    { key: "name", header: "名称", render: (c) => c.name },
    {
      key: "parentId",
      header: "上级分类",
      render: (c) => (categories.data ?? []).find((p) => p.id === c.parentId)?.name ?? "—",
    },
  ];

  return (
    <AdminShell
      title="题库分类"
      description="领域（domain）与应用场景（scenario）两套分类体系，支持层级；仅 Create + List。"
    >
      <div className="space-y-6">
        <CrudTable
          columns={columns}
          items={categories.data}
          isLoading={categories.isPending}
          loadError={categories.error}
          getRowId={(c) => c.id}
          schema={questionBankCategoryFormSchema}
          fields={fields}
          defaultValues={defaultValues}
          dialogTitle="新建题库分类"
          onCreate={(values) =>
            createQuestionBankCategory({
              ...values,
              parentId: values.parentId || null,
              description: values.description || null,
            })
          }
          onCreated={() => queryClient.invalidateQueries({ queryKey: ["admin", "categories"] })}
        />

        <TagQuestionCard categories={categories.data ?? []} />
      </div>
    </AdminShell>
  );
}
