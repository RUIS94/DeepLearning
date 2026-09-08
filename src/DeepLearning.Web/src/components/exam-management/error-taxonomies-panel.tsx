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

const columns: CrudColumn<ErrorTaxonomy>[] = [
  {
    key: "categoryKey",
    header: "Key",
    render: (t) => <span className="font-mono text-xs">{t.categoryKey}</span>,
  },
  { key: "categoryName", header: "Name", render: (t) => t.categoryName },
  { key: "description", header: "Description", render: (t) => t.description ?? "—" },
  { key: "exampleCases", header: "Edge cases", render: (t) => t.exampleCases ?? "—" },
];

const fields: CrudField<ErrorTaxonomyFormInput>[] = [
  { name: "categoryKey", label: "Category Key", kind: "text", placeholder: "distortion" },
  { name: "categoryName", label: "Name", kind: "text", placeholder: "Meaning distortion" },
  { name: "description", label: "Description (optional)", kind: "textarea" },
  {
    name: "exampleCases",
    label: "Edge cases (optional)",
    kind: "textarea",
    description:
      "Especially examples distinguishing easily-confused categories; rendered into the prompt as few-shot.",
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
      getRowId={(t) => t.id}
      schema={errorTaxonomyFormSchema}
      fields={fields}
      defaultValues={defaultValues}
      dialogTitle="New error taxonomy"
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
