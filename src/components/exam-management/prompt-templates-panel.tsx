"use client";

import { useQuery, useQueryClient } from "@tanstack/react-query";
import { CrudTable, type CrudColumn, type CrudField } from "@/components/admin/crud-table";
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
  { key: "layer", header: "层级", render: (t) => TemplateLayerLabel[t.layer] },
  {
    key: "examType",
    header: "考试类型",
    render: (t) =>
      t.examTypeId ? examTypeName(t.examTypeId) : <span className="text-muted-foreground">—</span>,
  },
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

// 稳定顺序：按枚举值升序展示各用途分组。
const templateTypeGroups = Object.entries(AiOperationTypeLabel)
  .map(([value, label]) => ({ value: Number(value), label }))
  .sort((a, b) => a.value - b.value);

export function PromptTemplatesPanel() {
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

  const allTemplates = templates.data;

  return (
    <div className="space-y-6">
      {templateTypeGroups.map((group) => (
        <section key={group.value}>
          <CrudTable
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
            // 从某个分组点「新建」时，预选对应用途。
            defaultValues={{ ...defaultValues, templateType: group.value }}
            dialogTitle={`新建 ${group.label} Prompt 模板`}
            createButtonLabel="新建"
            emptyMessage={`暂无「${group.label}」模板`}
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
        </section>
      ))}
    </div>
  );
}
