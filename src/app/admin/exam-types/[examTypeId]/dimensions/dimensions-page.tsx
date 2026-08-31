"use client";

import { useParams } from "next/navigation";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { AdminShell } from "@/components/shared/admin-shell";
import { CrudTable, type CrudColumn, type CrudField } from "@/components/admin/crud-table";
import { Badge } from "@/components/ui/badge";
import { createAssessmentDimension, listAssessmentDimensions } from "@/lib/api/exam-config";
import { ScaleTypeLabel, TaskTypeLabel } from "@/lib/types/enums";
import {
  assessmentDimensionFormSchema,
  type AssessmentDimensionFormInput,
} from "@/lib/validation/admin";
import type { AssessmentDimension } from "@/lib/types/dtos";
import { formatDate } from "@/lib/band";

const columns: CrudColumn<AssessmentDimension>[] = [
  {
    key: "dimensionKey",
    header: "Key",
    render: (d) => <span className="font-mono text-xs">{d.dimensionKey}</span>,
  },
  { key: "dimensionName", header: "名称", render: (d) => d.dimensionName },
  {
    key: "scaleType",
    header: "量表",
    render: (d) => <Badge variant="outline">{ScaleTypeLabel[d.scaleType]}</Badge>,
  },
  { key: "passThreshold", header: "通过线", render: (d) => d.passThreshold ?? "—" },
  {
    key: "applicableTaskType",
    header: "适用任务",
    render: (d) => (d.applicableTaskType === null ? "不限" : TaskTypeLabel[d.applicableTaskType]),
  },
  { key: "rubricVersion", header: "版本", render: (d) => d.rubricVersion },
  { key: "effectiveFrom", header: "生效自", render: (d) => formatDate(d.effectiveFrom) },
  {
    key: "effectiveTo",
    header: "生效至",
    render: (d) => (d.effectiveTo ? formatDate(d.effectiveTo) : "当前生效"),
  },
];

const fields: CrudField<AssessmentDimensionFormInput>[] = [
  { name: "dimensionKey", label: "Dimension Key", kind: "text", placeholder: "meaning_transfer" },
  { name: "dimensionName", label: "名称", kind: "text", placeholder: "意义传递" },
  {
    name: "scaleType",
    label: "量表类型",
    kind: "select",
    valueType: "number",
    options: Object.entries(ScaleTypeLabel).map(([v, l]) => ({ value: v, label: l })),
  },
  { name: "passThreshold", label: "通过线（可选）", kind: "text", placeholder: "Band 2 or above" },
  {
    name: "applicableTaskType",
    label: "适用任务类型",
    kind: "select",
    valueType: "number",
    options: [
      { value: "-1", label: "不限（两种任务类型均适用）" },
      ...Object.entries(TaskTypeLabel).map(([v, l]) => ({ value: v, label: l })),
    ],
  },
  { name: "levelDescriptions", label: "各 Band 完整英文原文", kind: "textarea", rows: 5 },
  { name: "rubricVersion", label: "Rubric 版本号", kind: "text", placeholder: "2026-02" },
  {
    name: "effectiveFrom",
    label: "生效日期",
    kind: "date",
    description: "若该 dimensionKey 已有生效版本，新版本会自动关闭旧版本（方案 §3.8）。",
  },
  { name: "sourceReference", label: "官方来源（可选）", kind: "text" },
];

const defaultValues: AssessmentDimensionFormInput = {
  dimensionKey: "",
  dimensionName: "",
  scaleType: 0,
  passThreshold: "",
  applicableTaskType: -1,
  levelDescriptions: "",
  rubricVersion: "",
  effectiveFrom: "",
  sourceReference: "",
};

export function DimensionsPage() {
  const { examTypeId } = useParams<{ examTypeId: string }>();
  const queryClient = useQueryClient();
  const dimensions = useQuery({
    queryKey: ["admin", "dimensions", examTypeId],
    queryFn: () => listAssessmentDimensions(examTypeId),
  });

  return (
    <AdminShell
      title="评分维度"
      description="仅展示当前生效窗口内的版本；新建即“修订生效日期”，语义参见方案 §3.8。"
    >
      <CrudTable
        columns={columns}
        items={dimensions.data}
        isLoading={dimensions.isPending}
        loadError={dimensions.error}
        getRowId={(d) => d.id}
        schema={assessmentDimensionFormSchema}
        fields={fields}
        defaultValues={defaultValues}
        dialogTitle="新建评分维度版本"
        createButtonLabel="新建版本"
        onCreate={(values) =>
          createAssessmentDimension(examTypeId, {
            ...values,
            passThreshold: values.passThreshold || null,
            applicableTaskType:
              values.applicableTaskType === -1 || values.applicableTaskType == null
                ? null
                : values.applicableTaskType,
            sourceReference: values.sourceReference || null,
          })
        }
        onCreated={() =>
          queryClient.invalidateQueries({ queryKey: ["admin", "dimensions", examTypeId] })
        }
      />
    </AdminShell>
  );
}
