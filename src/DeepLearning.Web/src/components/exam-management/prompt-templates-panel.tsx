"use client";

import type { Ref } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import {
  CrudTable,
  type CrudColumn,
  type CrudCreateHandle,
  type CrudField,
} from "@/components/admin/crud-table";
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

// 「用途」不再单独成列——每个 templateType 拆成一张独立的表，用途写在小标题上。
const buildColumns = (examTypeName: (id: string) => string): CrudColumn<PromptTemplate>[] => [
  { key: "layer", header: "Layer", render: (t) => TemplateLayerLabel[t.layer] },
  {
    key: "examType",
    header: "Exam type",
    render: (t) =>
      t.examTypeId ? examTypeName(t.examTypeId) : <span className="text-muted-foreground">—</span>,
  },
  {
    key: "scope",
    header: "Association",
    render: (t) =>
      t.examTypeId
        ? "Exam-type specific"
        : `Subject: ${SubjectCategoryLabel[t.subjectCategory ?? 0]}`,
  },
  { key: "version", header: "Version", render: (t) => `v${t.version}` },
  {
    key: "isActive",
    header: "Status",
    render: (t) =>
      t.isActive ? (
        <Badge variant="outline" className="border-transparent bg-success/12 text-success">
          Active
        </Badge>
      ) : (
        <Badge variant="outline" className="text-muted-foreground">
          Disabled
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

// 稳定顺序：按枚举值升序展示各用途分组。
const templateTypeGroups = Object.entries(AiOperationTypeLabel)
  .map(([value, label]) => ({ value: Number(value), label }))
  .sort((a, b) => a.value - b.value);

export function PromptTemplatesPanel({ createRef }: { createRef?: Ref<CrudCreateHandle> }) {
  const queryClient = useQueryClient();
  const examTypes = useQuery({ queryKey: ["admin", "exam-types"], queryFn: listExamTypes });

  const listKey = ["admin", "prompt-templates"];
  const templates = useQuery({
    queryKey: listKey,
    // 不传 isActive -> 后端返回全部(含停用)，管理页需要看得到停用的行
    queryFn: () => listPromptTemplates(),
  });
  const invalidate = () =>
    queryClient.invalidateQueries({ queryKey: ["admin", "prompt-templates"] });

  const examTypeName = (id: string) => (examTypes.data ?? []).find((e) => e.id === id)?.name ?? id;
  const columns = buildColumns(examTypeName);

  const fields: CrudField<PromptTemplateFormInput>[] = [
    {
      name: "examTypeId",
      label: "Associated exam type (choose either this or subject; not editable)",
      kind: "select",
      options: [
        { value: "", label: "None (shared by subject)" },
        ...(examTypes.data ?? []).map((e) => ({ value: e.id, label: e.name })),
      ],
    },
    {
      name: "subjectCategory",
      label: "Associated subject category (choose either this or exam type; not editable)",
      kind: "select",
      valueType: "number",
      options: [
        { value: "-1", label: "None (exam-type specific)" },
        ...Object.entries(SubjectCategoryLabel).map(([v, l]) => ({ value: v, label: l })),
      ],
    },
    {
      name: "templateType",
      label: "Template purpose (not editable)",
      kind: "select",
      valueType: "number",
      options: Object.entries(AiOperationTypeLabel).map(([v, l]) => ({ value: v, label: l })),
    },
    {
      name: "layer",
      label: "Layer (not editable)",
      kind: "select",
      valueType: "number",
      options: Object.entries(TemplateLayerLabel).map(([v, l]) => ({ value: v, label: l })),
    },
    { name: "templateContent", label: "Template content (Scriban)", kind: "textarea", rows: 8 },
    {
      name: "version",
      label: "Version",
      kind: "number",
      description:
        "The backend does not auto-increment; you must keep versions increasing within the same association + purpose + layer.",
    },
    { name: "isActive", label: "Enabled", kind: "switch" },
  ];

  const allTemplates = templates.data;

  const createTemplate = (values: PromptTemplateFormInput) =>
    createPromptTemplate({
      examTypeId: values.examTypeId || null,
      subjectCategory: values.subjectCategory === -1 ? null : (values.subjectCategory ?? null),
      templateType: values.templateType,
      layer: values.layer,
      templateContent: values.templateContent,
      version: values.version,
    });

  return (
    <div className="space-y-6">
      {/* 新建按钮提到了 Tab 同行，这里只留一个不预选用途的弹窗入口。 */}
      <CrudTable
        dialogOnly
        openCreateRef={createRef}
        isLoading={false}
        schema={promptTemplateFormSchema}
        fields={fields}
        defaultValues={defaultValues}
        dialogTitle="New prompt template"
        onCreate={createTemplate}
        onChanged={invalidate}
      />

      {templateTypeGroups.map((group) => (
        <section key={group.value}>
          <CrudTable
            hideCreate
            title={<h3 className="text-sm font-semibold">{group.label}</h3>}
            columns={columns}
            items={
              allTemplates ? allTemplates.filter((t) => t.templateType === group.value) : undefined
            }
            isLoading={templates.isPending}
            loadError={templates.error}
            getRowId={(t) => t.id}
            schema={promptTemplateFormSchema}
            fields={fields}
            // 后端 PUT /prompt-templates/{id} 只更新 templateContent/version/isActive，
            // 关联与用途/分层改了也不会生效——编辑时置灰，避免用户白改一场。
            lockOnEdit={["examTypeId", "subjectCategory", "templateType", "layer"]}
            defaultValues={{ ...defaultValues, templateType: group.value }}
            dialogTitle={`New ${group.label} prompt template`}
            emptyMessage={`No "${group.label}" templates yet`}
            onCreate={createTemplate}
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
              title: "Delete this prompt template?",
              description:
                "Hard delete, irreversible. To just deactivate, use Edit and turn off Enabled.",
            })}
            onChanged={invalidate}
          />
        </section>
      ))}
    </div>
  );
}
