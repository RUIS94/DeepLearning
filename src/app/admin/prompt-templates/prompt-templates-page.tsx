"use client";

import { useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { AdminShell } from "@/components/shared/admin-shell";
import { CrudTable, type CrudColumn, type CrudField } from "@/components/admin/crud-table";
import { EnumSelect } from "@/components/shared/enum-select";
import { Badge } from "@/components/ui/badge";
import { createPromptTemplate, listExamTypes, listPromptTemplates } from "@/lib/mock/store";
import { SubjectCategoryLabel, TemplateLayerLabel, TemplateTypeLabel } from "@/lib/types/enums";
import { promptTemplateFormSchema, type PromptTemplateFormInput } from "@/lib/validation/admin";
import type { PromptTemplate } from "@/lib/types/dtos";
import { formatDate } from "@/lib/band";

const columns: CrudColumn<PromptTemplate>[] = [
  {
    key: "templateType",
    header: "用途",
    render: (t) => <Badge variant="outline">{TemplateTypeLabel[t.templateType]}</Badge>,
  },
  { key: "layer", header: "层级", render: (t) => TemplateLayerLabel[t.layer] },
  {
    key: "scope",
    header: "关联",
    render: (t) =>
      t.examTypeId
        ? `考试类型：${t.examTypeId}`
        : `学科：${SubjectCategoryLabel[t.subjectCategory ?? 0]}`,
  },
  { key: "version", header: "版本", render: (t) => `v${t.version}` },
  { key: "isActive", header: "状态", render: (t) => (t.isActive ? "生效中" : "已停用") },
  { key: "createdAt", header: "创建时间", render: (t) => formatDate(t.createdAt) },
];

const defaultValues: PromptTemplateFormInput = {
  examTypeId: "",
  subjectCategory: 0,
  templateType: 0,
  layer: 0,
  templateContent: "",
};

export function PromptTemplatesPage() {
  const queryClient = useQueryClient();
  const examTypes = useQuery({ queryKey: ["admin", "exam-types"], queryFn: listExamTypes });
  const [templateType, setTemplateType] = useState<number | "all">("all");

  const templates = useQuery({
    queryKey: ["admin", "prompt-templates", templateType],
    queryFn: () =>
      listPromptTemplates({ templateType: templateType === "all" ? undefined : templateType }),
  });

  const fields: CrudField<PromptTemplateFormInput>[] = [
    {
      name: "examTypeId",
      label: "关联考试类型（与学科二选一）",
      kind: "select",
      options: [
        { value: "", label: "不关联（按学科共享）" },
        ...(examTypes.data ?? []).map((e) => ({ value: e.id, label: e.name })),
      ],
    },
    {
      name: "subjectCategory",
      label: "关联学科类别（与考试类型二选一）",
      kind: "select",
      valueType: "number",
      options: [
        { value: "-1", label: "不关联（考试类型专属）" },
        ...Object.entries(SubjectCategoryLabel).map(([v, l]) => ({ value: v, label: l })),
      ],
    },
    {
      name: "templateType",
      label: "模板用途",
      kind: "select",
      valueType: "number",
      options: Object.entries(TemplateTypeLabel).map(([v, l]) => ({ value: v, label: l })),
    },
    {
      name: "layer",
      label: "分层",
      kind: "select",
      valueType: "number",
      options: Object.entries(TemplateLayerLabel).map(([v, l]) => ({ value: v, label: l })),
    },
    {
      name: "templateContent",
      label: "模板正文（Scriban）",
      kind: "textarea",
      rows: 8,
      description: "渲染时按考试类型加载，共享方法论片段与考试类型专属片段分层拼装。",
    },
  ];

  return (
    <AdminShell
      title="Prompt 模板"
      description="出题/评分/追问/标准修订的系统提示词，按考试类型独立维护、独立版本管理。"
      actions={
        <EnumSelect
          labels={TemplateTypeLabel}
          value={templateType}
          onChange={setTemplateType}
          allowAll
          allLabel="全部用途"
          placeholder="用途"
          className="w-36"
        />
      }
    >
      <CrudTable
        columns={columns}
        items={templates.data}
        isLoading={templates.isPending}
        loadError={templates.error}
        getRowId={(t) => t.id}
        schema={promptTemplateFormSchema}
        fields={fields}
        defaultValues={defaultValues}
        dialogTitle="新建 Prompt 模板"
        onCreate={(values) =>
          createPromptTemplate({
            examTypeId: values.examTypeId || null,
            subjectCategory:
              values.subjectCategory === -1 ? null : (values.subjectCategory ?? null),
            templateType: values.templateType,
            layer: values.layer,
            templateContent: values.templateContent,
          })
        }
        onCreated={() => queryClient.invalidateQueries({ queryKey: ["admin", "prompt-templates"] })}
      />
    </AdminShell>
  );
}
