"use client";

import { useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { CrudTable, type CrudColumn, type CrudField } from "@/components/admin/crud-table";
import { EnumSelect } from "@/components/shared/enum-select";
import { Badge } from "@/components/ui/badge";
import {
  createPromptTemplate,
  deletePromptTemplate,
  listExamTypes,
  listPromptTemplates,
  updatePromptTemplate,
} from "@/lib/api/exam-config";
import { AiOperationTypeLabel, SubjectCategoryLabel, TemplateLayerLabel } from "@/lib/types/enums";
import { promptTemplateFormSchema, type PromptTemplateFormInput } from "@/lib/validation/admin";
import type { PromptTemplate } from "@/lib/types/dtos";

const columns: CrudColumn<PromptTemplate>[] = [
  {
    key: "templateType",
    header: "用途",
    render: (t) => <Badge variant="outline">{AiOperationTypeLabel[t.templateType]}</Badge>,
  },
  { key: "layer", header: "层级", render: (t) => TemplateLayerLabel[t.layer] },
  {
    key: "scope",
    header: "关联",
    render: (t) =>
      t.examTypeId ? "考试类型专属" : `学科：${SubjectCategoryLabel[t.subjectCategory ?? 0]}`,
  },
  { key: "version", header: "版本", render: (t) => `v${t.version}` },
  {
    key: "isActive",
    header: "状态",
    render: (t) =>
      t.isActive ? (
        <Badge variant="outline" className="border-transparent bg-success/12 text-success">
          生效中
        </Badge>
      ) : (
        <Badge variant="outline" className="text-muted-foreground">
          已停用
        </Badge>
      ),
  },
];

const defaultValues: PromptTemplateFormInput = {
  examTypeId: "",
  subjectCategory: 0,
  templateType: 0,
  layer: 0,
  templateContent: "",
  version: 1,
  isActive: true,
};

export function PromptTemplatesPanel() {
  const queryClient = useQueryClient();
  const examTypes = useQuery({ queryKey: ["admin", "exam-types"], queryFn: listExamTypes });
  const [templateType, setTemplateType] = useState<number | "all">("all");

  const listKey = ["admin", "prompt-templates", templateType];
  const templates = useQuery({
    queryKey: listKey,
    // 不传 isActive -> 后端返回全部(含停用)，管理页需要看得到停用的行
    queryFn: () =>
      listPromptTemplates({ templateType: templateType === "all" ? undefined : templateType }),
  });
  const invalidate = () =>
    queryClient.invalidateQueries({ queryKey: ["admin", "prompt-templates"] });

  const fields: CrudField<PromptTemplateFormInput>[] = [
    {
      name: "examTypeId",
      label: "关联考试类型（与学科二选一，编辑不生效）",
      kind: "select",
      options: [
        { value: "", label: "不关联（按学科共享）" },
        ...(examTypes.data ?? []).map((e) => ({ value: e.id, label: e.name })),
      ],
    },
    {
      name: "subjectCategory",
      label: "关联学科类别（与考试类型二选一，编辑不生效）",
      kind: "select",
      valueType: "number",
      options: [
        { value: "-1", label: "不关联（考试类型专属）" },
        ...Object.entries(SubjectCategoryLabel).map(([v, l]) => ({ value: v, label: l })),
      ],
    },
    {
      name: "templateType",
      label: "模板用途（编辑不生效）",
      kind: "select",
      valueType: "number",
      options: Object.entries(AiOperationTypeLabel).map(([v, l]) => ({ value: v, label: l })),
    },
    {
      name: "layer",
      label: "分层（编辑不生效）",
      kind: "select",
      valueType: "number",
      options: Object.entries(TemplateLayerLabel).map(([v, l]) => ({ value: v, label: l })),
    },
    { name: "templateContent", label: "模板正文（Scriban）", kind: "textarea", rows: 8 },
    {
      name: "version",
      label: "版本号",
      kind: "number",
      description: "后端不自动递增；同一关联 + 用途 + 分层下需自行保证递增。",
    },
    { name: "isActive", label: "启用", kind: "switch" },
  ];

  return (
    <div className="space-y-4">
      <div className="flex justify-end">
        <EnumSelect
          labels={AiOperationTypeLabel}
          value={templateType}
          onChange={setTemplateType}
          allowAll
          allLabel="全部用途"
          placeholder="用途"
          className="w-36"
        />
      </div>
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
            version: values.version,
          })
        }
        toFormValues={(t) => ({
          examTypeId: t.examTypeId ?? "",
          subjectCategory: t.subjectCategory ?? -1,
          templateType: t.templateType,
          layer: t.layer,
          templateContent: t.templateContent,
          version: t.version,
          isActive: t.isActive,
        })}
        onUpdate={(id, values) =>
          updatePromptTemplate(id, {
            templateContent: values.templateContent,
            version: values.version,
            isActive: values.isActive ?? true,
          })
        }
        onDelete={(id) => deletePromptTemplate(id)}
        deleteConfirm={() => ({
          title: "删除这条 Prompt 模板？",
          description: "硬删除，不可撤销。若只是想停用，改用「编辑」把「启用」关掉。",
        })}
        onChanged={invalidate}
      />
    </div>
  );
}
