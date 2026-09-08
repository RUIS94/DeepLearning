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
  { key: "dimensionName", header: "Name", render: (d) => d.dimensionName },
  {
    key: "scaleType",
    header: "Scale",
    render: (d) => <Badge variant="outline">{ScaleTypeLabel[d.scaleType]}</Badge>,
  },
  { key: "passThreshold", header: "Pass line", render: (d) => d.passThreshold ?? "—" },
  { key: "rubricVersion", header: "Version", render: (d) => d.rubricVersion },
  { key: "effectiveFrom", header: "Effective from", render: (d) => formatDate(d.effectiveFrom) },
];

const fields: CrudField<AssessmentDimensionFormInput>[] = [
  { name: "dimensionKey", label: "Dimension Key", kind: "text", placeholder: "meaning_transfer" },
  { name: "dimensionName", label: "Name", kind: "text", placeholder: "Meaning Transfer" },
  {
    name: "scaleType",
    label: "Scale type",
    kind: "select",
    valueType: "number",
    options: Object.entries(ScaleTypeLabel).map(([v, l]) => ({ value: v, label: l })),
  },
  {
    name: "passThreshold",
    label: "Pass line (optional)",
    kind: "text",
    placeholder: "Band 2 or above",
  },
  {
    name: "applicableTaskType",
    label: "Applicable task type",
    kind: "select",
    valueType: "number",
    options: [
      { value: "-1", label: "Any (applies to both task types)" },
      ...Object.entries(TaskTypeLabel).map(([v, l]) => ({ value: v, label: l })),
    ],
  },
  {
    name: "levelDescriptions",
    label: "Full original English text for each Band",
    kind: "textarea",
    rows: 5,
  },
  { name: "rubricVersion", label: "Rubric version", kind: "text", placeholder: "2026-02" },
  {
    name: "effectiveFrom",
    label: "Effective date",
    kind: "date",
    description:
      "If this dimensionKey already has an active version, the new version automatically closes the old one",
  },
  { name: "sourceReference", label: "Official source (optional)", kind: "text" },
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

/** level_descriptions 是 jsonb 字符串，展开时转成 Band => 原文 的 key-value 组显示。 */
function LevelDescriptions({ raw }: { raw: string }) {
  let entries: [string, string][] = [];
  try {
    const parsed = JSON.parse(raw) as Record<string, unknown>;
    entries = Object.entries(parsed).map(([k, v]) => [k, String(v)]);
  } catch {
    return <pre className="whitespace-pre-wrap text-xs text-muted-foreground">{raw}</pre>;
  }
  if (entries.length === 0) {
    return <p className="text-xs text-muted-foreground">(no level_descriptions)</p>;
  }
  return (
    <dl className="space-y-2 py-1 text-sm">
      {entries.map(([band, text]) => (
        <div key={band} className="grid grid-cols-[4rem_1fr] gap-3">
          <dt className="font-medium text-muted-foreground">Band {band}</dt>
          <dd className="leading-relaxed">{text}</dd>
        </div>
      ))}
    </dl>
  );
}

export function DimensionsPanel({
  examTypeId,
  createRef,
}: {
  examTypeId: string;
  createRef?: Ref<CrudCreateHandle>;
}) {
  const queryClient = useQueryClient();
  const key = ["admin", "dimensions", examTypeId];
  const dimensions = useQuery({
    queryKey: key,
    queryFn: () => listAssessmentDimensions(examTypeId),
  });
  const invalidate = () => queryClient.invalidateQueries({ queryKey: key });

  const all = dimensions.data ?? [];
  const taskA = all.filter((d) => d.applicableTaskType !== 1);
  const taskB = all.filter((d) => d.applicableTaskType !== 0);

  const create = (values: AssessmentDimensionFormInput) =>
    createAssessmentDimension(examTypeId, {
      ...values,
      passThreshold: values.passThreshold || null,
      applicableTaskType:
        values.applicableTaskType === -1 || values.applicableTaskType == null
          ? null
          : values.applicableTaskType,
      sourceReference: values.sourceReference || null,
    });

  return (
    <div className="space-y-8">
      <section className="space-y-2">
        <h3 className="text-sm font-semibold">TaskA dimensions</h3>
        <CrudTable
          openCreateRef={createRef}
          hideCreate
          columns={columns}
          items={dimensions.isPending ? undefined : taskA}
          isLoading={dimensions.isPending}
          loadError={dimensions.error}
          getRowId={(d) => d.id}
          schema={assessmentDimensionFormSchema}
          fields={fields}
          defaultValues={defaultValues}
          dialogTitle="New scoring-dimension version"
          renderExpanded={(d) => <LevelDescriptions raw={d.levelDescriptions} />}
          onCreate={create}
          onChanged={invalidate}
          emptyMessage="No dimensions apply to TaskA"
        />
      </section>

      <section className="space-y-2">
        <h3 className="text-sm font-semibold">TaskB dimensions</h3>
        <CrudTable
          columns={columns}
          items={dimensions.isPending ? undefined : taskB}
          isLoading={dimensions.isPending}
          loadError={dimensions.error}
          getRowId={(d) => d.id}
          schema={assessmentDimensionFormSchema}
          fields={fields}
          defaultValues={defaultValues}
          hideCreate
          renderExpanded={(d) => <LevelDescriptions raw={d.levelDescriptions} />}
          onCreate={create}
          onChanged={invalidate}
          emptyMessage="No dimensions apply to TaskB"
        />
      </section>
    </div>
  );
}
