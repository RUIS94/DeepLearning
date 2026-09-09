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
import { promptTemplateFormSchema, type PromptTemplateFormInput } from "@/lib/validation/admin";
import type { PromptTemplate } from "@/lib/types/dtos";
import { AiOperationType } from "@/lib/types/enums";
import { useT, type TranslateFn } from "@/lib/i18n";
import { useEnumLabels, type EnumLabels } from "@/lib/i18n/enum-labels";

// 「用途」不再单独成列——每个 templateType 拆成一张独立的表，用途写在小标题上。
const buildColumns = (
  t: TranslateFn,
  labels: Pick<EnumLabels, "TemplateLayerLabel" | "SubjectCategoryLabel">,
  examTypeName: (id: string) => string,
): CrudColumn<PromptTemplate>[] => [
  {
    key: "layer",
    header: t("examMgmt.tpl.colLayer"),
    render: (row) => labels.TemplateLayerLabel[row.layer],
  },
  {
    key: "examType",
    header: t("examMgmt.tpl.colExamType"),
    render: (row) =>
      row.examTypeId ? (
        examTypeName(row.examTypeId)
      ) : (
        <span className="text-muted-foreground">—</span>
      ),
  },
  {
    key: "scope",
    header: t("examMgmt.tpl.colAssociation"),
    render: (row) =>
      row.examTypeId
        ? t("examMgmt.tpl.examTypeSpecific")
        : t("examMgmt.tpl.subjectPrefix", {
            subject: labels.SubjectCategoryLabel[row.subjectCategory ?? 0] ?? "",
          }),
  },
  { key: "version", header: t("examMgmt.tpl.colVersion"), render: (row) => `v${row.version}` },
  {
    key: "isActive",
    header: t("common.status"),
    render: (row) =>
      row.isActive ? (
        <Badge variant="outline" className="border-success/40 text-success">
          {t("examMgmt.tpl.active")}
        </Badge>
      ) : (
        <Badge variant="outline" className="border-destructive/40 text-destructive">
          {t("examMgmt.tpl.disabled")}
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

export function PromptTemplatesPanel({
  examTypeId,
  createRef,
}: {
  examTypeId?: string;
  createRef?: Ref<CrudCreateHandle>;
}) {
  const t = useT();
  const { AiOperationTypeLabel, SubjectCategoryLabel, TemplateLayerLabel } = useEnumLabels();
  // 稳定顺序：按下面这份业务排序展示各用途分组（未列出的类型排在末尾，按枚举值兜底）。
  const groupOrder: number[] = [
    AiOperationType.question_gen,
    AiOperationType.grading,
    AiOperationType.followup,
    AiOperationType.followup_summary,
    AiOperationType.score_challenge_summary,
    AiOperationType.deep_learning,
    AiOperationType.vocab_semantic_drift,
    AiOperationType.weak_point_classification,
    AiOperationType.weak_point_detection_criteria,
    AiOperationType.weak_point_recheck,
    AiOperationType.progress_trend,
    AiOperationType.standard_revision,
  ];
  const rank = (value: number) => {
    const i = groupOrder.indexOf(value);
    return i === -1 ? groupOrder.length + value : i;
  };
  const templateTypeGroups = Object.entries(AiOperationTypeLabel)
    .map(([value, label]) => ({ value: Number(value), label }))
    .sort((a, b) => rank(a.value) - rank(b.value));
  const queryClient = useQueryClient();
  const examTypes = useQuery({ queryKey: ["admin", "exam-types"], queryFn: listExamTypes });

  const listKey = ["admin", "prompt-templates", examTypeId ?? null];
  const templates = useQuery({
    queryKey: listKey,
    // 不传 isActive -> 后端返回全部(含停用)，管理页需要看得到停用的行。
    // 传 examTypeId + includeGlobalScope -> 当前考试类型的行 + 共享(exam_type_id IS NULL)的行。
    queryFn: () =>
      listPromptTemplates(examTypeId ? { examTypeId, includeGlobalScope: true } : undefined),
  });
  const invalidate = () =>
    queryClient.invalidateQueries({ queryKey: ["admin", "prompt-templates"] });

  const examTypeName = (id: string) => (examTypes.data ?? []).find((e) => e.id === id)?.name ?? id;
  const columns = buildColumns(t, { TemplateLayerLabel, SubjectCategoryLabel }, examTypeName);
  // 新建时默认挂到当前考试类型（仍可在表单里改成"共享"或其它）。
  const defaults: PromptTemplateFormInput = { ...defaultValues, examTypeId: examTypeId ?? "" };

  const fields: CrudField<PromptTemplateFormInput>[] = [
    {
      name: "examTypeId",
      label: t("examMgmt.tpl.fieldExamType"),
      kind: "select",
      options: [
        { value: "", label: t("examMgmt.tpl.examTypeNone") },
        ...(examTypes.data ?? []).map((e) => ({ value: e.id, label: e.name })),
      ],
    },
    {
      name: "subjectCategory",
      label: t("examMgmt.tpl.fieldSubject"),
      kind: "select",
      valueType: "number",
      options: [
        { value: "-1", label: t("examMgmt.tpl.subjectNone") },
        ...Object.entries(SubjectCategoryLabel).map(([v, l]) => ({ value: v, label: l })),
      ],
    },
    {
      name: "templateType",
      label: t("examMgmt.tpl.fieldPurpose"),
      kind: "select",
      valueType: "number",
      options: Object.entries(AiOperationTypeLabel).map(([v, l]) => ({ value: v, label: l })),
    },
    {
      name: "layer",
      label: t("examMgmt.tpl.fieldLayer"),
      kind: "select",
      valueType: "number",
      options: Object.entries(TemplateLayerLabel).map(([v, l]) => ({ value: v, label: l })),
    },
    {
      name: "templateContent",
      label: t("examMgmt.tpl.fieldContent"),
      kind: "textarea",
      rows: 8,
    },
    {
      name: "version",
      label: t("examMgmt.tpl.fieldVersion"),
      kind: "number",
      description: t("examMgmt.tpl.versionHint"),
    },
    { name: "isActive", label: t("examMgmt.tpl.fieldEnabled"), kind: "switch" },
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
      <p className="text-xs text-muted-foreground">{t("examMgmt.sharedNotice")}</p>

      {/* 新建按钮提到了 Tab 同行，这里只留一个不预选用途的弹窗入口。 */}
      <CrudTable
        dialogOnly
        openCreateRef={createRef}
        isLoading={false}
        schema={promptTemplateFormSchema}
        fields={fields}
        defaultValues={defaults}
        dialogTitle={t("examMgmt.tpl.dialogTitle")}
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
              allTemplates
                ? allTemplates.filter((row) => row.templateType === group.value)
                : undefined
            }
            isLoading={templates.isPending}
            loadError={templates.error}
            getRowId={(row) => row.id}
            schema={promptTemplateFormSchema}
            fields={fields}
            // 后端 PUT /prompt-templates/{id} 只更新 templateContent/version/isActive，
            // 关联与用途/分层改了也不会生效——编辑时置灰，避免用户白改一场。
            lockOnEdit={["examTypeId", "subjectCategory", "templateType", "layer"]}
            defaultValues={{ ...defaults, templateType: group.value }}
            dialogTitle={t("examMgmt.tpl.dialogTitleGroup", { group: group.label })}
            emptyMessage={t("examMgmt.tpl.emptyGroup", { group: group.label })}
            onCreate={createTemplate}
            toFormValues={(row) => ({
              examTypeId: row.examTypeId ?? "",
              subjectCategory: row.subjectCategory ?? -1,
              templateType: row.templateType,
              layer: row.layer,
              templateContent: row.templateContent,
              version: row.version,
              isActive: row.isActive,
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
              title: t("examMgmt.tpl.deleteTitle"),
              description: t("examMgmt.tpl.deleteDesc"),
            })}
            onChanged={invalidate}
          />
        </section>
      ))}
    </div>
  );
}
