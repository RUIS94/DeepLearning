"use client";

import type { Ref } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import {
  CrudTable,
  type CrudColumn,
  type CrudCreateHandle,
  type CrudField,
} from "@/components/admin/crud-table";
import { createErrorTaxonomy, listErrorTaxonomiesByExamType } from "@/lib/api/exam-config";
import { errorTaxonomyFormSchema, type ErrorTaxonomyFormInput } from "@/lib/validation/admin";
import type { ErrorTaxonomy } from "@/lib/types/dtos";
import { useT, type TranslateFn } from "@/lib/i18n";

const buildColumns = (t: TranslateFn): CrudColumn<ErrorTaxonomy>[] => [
  {
    key: "categoryKey",
    header: "Key",
    render: (row) => <span className="font-mono text-xs">{row.categoryKey}</span>,
  },
  { key: "categoryName", header: t("common.name"), render: (row) => row.categoryName },
  {
    key: "description",
    header: t("common.description"),
    render: (row) => row.description ?? "—",
  },
  {
    key: "exampleCases",
    header: t("examMgmt.tax.colEdgeCases"),
    render: (row) => row.exampleCases ?? "—",
  },
];

const buildFields = (t: TranslateFn): CrudField<ErrorTaxonomyFormInput>[] => [
  { name: "categoryKey", label: "Category Key", kind: "text", placeholder: "distortion" },
  {
    name: "categoryName",
    label: t("common.name"),
    kind: "text",
    placeholder: "Meaning distortion",
  },
  { name: "description", label: t("examMgmt.tax.fieldDescription"), kind: "textarea" },
  {
    name: "exampleCases",
    label: t("examMgmt.tax.fieldEdgeCases"),
    kind: "textarea",
    description: t("examMgmt.tax.edgeCasesHint"),
  },
];

const defaultValues: ErrorTaxonomyFormInput = {
  categoryKey: "",
  categoryName: "",
  description: "",
  exampleCases: "",
};

export function ErrorTaxonomiesPanel({
  examTypeId,
  createRef,
}: {
  examTypeId: string;
  createRef?: Ref<CrudCreateHandle>;
}) {
  const t = useT();
  const columns = buildColumns(t);
  const fields = buildFields(t);
  const queryClient = useQueryClient();
  const key = ["admin", "error-taxonomies", examTypeId];
  const taxonomies = useQuery({
    queryKey: key,
    queryFn: () => listErrorTaxonomiesByExamType(examTypeId),
  });

  return (
    <CrudTable
      openCreateRef={createRef}
      hideCreate
      columns={columns}
      items={taxonomies.data}
      isLoading={taxonomies.isPending}
      loadError={taxonomies.error}
      getRowId={(row) => row.id}
      schema={errorTaxonomyFormSchema}
      fields={fields}
      defaultValues={defaultValues}
      dialogTitle={t("examMgmt.tax.dialogTitle")}
      onCreate={(values) =>
        createErrorTaxonomy(examTypeId, {
          ...values,
          description: values.description || null,
          exampleCases: values.exampleCases || null,
        })
      }
      onChanged={() => queryClient.invalidateQueries({ queryKey: key })}
    />
  );
}
