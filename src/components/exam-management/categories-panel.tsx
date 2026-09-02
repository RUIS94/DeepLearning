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

export function CategoriesPanel({ createRef }: { createRef?: Ref<CrudCreateHandle> }) {
  const queryClient = useQueryClient();
  const categories = useQuery({ queryKey: ["admin", "categories"], queryFn: listCategories });
  const invalidate = () => {
    queryClient.invalidateQueries({ queryKey: ["admin", "categories"] });
    queryClient.invalidateQueries({ queryKey: ["categories"] });
  };

  const fields: CrudField<QuestionBankCategoryFormInput>[] = [
    {
      name: "categoryType",
      label: "分类体系（新建时生效，编辑不可改）",
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
    { key: "description", header: "描述", render: (c) => c.description ?? "—" },
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
        dialogTitle="新建题库分类"
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
          title: `删除分类「${c.name}」？`,
          description: "若该分类有子分类或已被题目引用，后端会拒绝（返回冲突）。",
        })}
        onChanged={invalidate}
      />

      <TagQuestionCard categories={categories.data ?? []} />
    </div>
  );
}
