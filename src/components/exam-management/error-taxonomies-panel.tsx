"use client";

import { useQuery, useQueryClient } from "@tanstack/react-query";
import { CrudTable, type CrudColumn, type CrudField } from "@/components/admin/crud-table";
import { createErrorTaxonomy, listErrorTaxonomiesByExamType } from "@/lib/api/exam-config";
import { errorTaxonomyFormSchema, type ErrorTaxonomyFormInput } from "@/lib/validation/admin";
import type { ErrorTaxonomy } from "@/lib/types/dtos";

const columns: CrudColumn<ErrorTaxonomy>[] = [
  {
    key: "categoryKey",
    header: "Key",
    render: (t) => <span className="font-mono text-xs">{t.categoryKey}</span>,
  },
  { key: "categoryName", header: "名称", render: (t) => t.categoryName },
  { key: "description", header: "说明", render: (t) => t.description ?? "—" },
  { key: "exampleCases", header: "边界案例", render: (t) => t.exampleCases ?? "—" },
];

const fields: CrudField<ErrorTaxonomyFormInput>[] = [
  { name: "categoryKey", label: "Category Key", kind: "text", placeholder: "distortion" },
  { name: "categoryName", label: "名称", kind: "text", placeholder: "意义扭曲" },
  { name: "description", label: "说明（可选）", kind: "textarea" },
  {
    name: "exampleCases",
    label: "边界案例（可选）",
    kind: "textarea",
    description: "尤其是容易混淆的类别之间的区分举例，会作为 few-shot 渲染进 prompt。",
  },
];

const defaultValues: ErrorTaxonomyFormInput = {
  categoryKey: "",
  categoryName: "",
  description: "",
  exampleCases: "",
};

export function ErrorTaxonomiesPanel({ examTypeId }: { examTypeId: string }) {
  const queryClient = useQueryClient();
  const key = ["admin", "error-taxonomies", examTypeId];
  const taxonomies = useQuery({
    queryKey: key,
    queryFn: () => listErrorTaxonomiesByExamType(examTypeId),
  });

  return (
    <CrudTable
      columns={columns}
      items={taxonomies.data}
      isLoading={taxonomies.isPending}
      loadError={taxonomies.error}
      getRowId={(t) => t.id}
      schema={errorTaxonomyFormSchema}
      fields={fields}
      defaultValues={defaultValues}
      dialogTitle="新建错误分类"
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
