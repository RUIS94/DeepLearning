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
import { qk } from "@/lib/query-keys";

const buildColumns = (t: TranslateFn): CrudColumn<ErrorTaxonomy>[] => [
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
  // 合并前这里自成一个 ["admin","error-taxonomies",examTypeId] 命名空间，跟
  // useErrorTaxonomies(hooks/use-exam-config.ts)读同一个 listErrorTaxonomiesByExamType(examTypeId)
  // 接口却用 ["exam-config","error-taxonomies",examTypeId]——在这里新建错误类别不会让
  // import-question-panel 等消费 useErrorTaxonomies 的地方失效。现在共用一个
  // qk.examConfigErrorTaxonomies 命名空间（代码复用扫描_07_优化计划.md §4.1b）。
  const taxonomies = useQuery({
    queryKey: qk.examConfigErrorTaxonomies(examTypeId),
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
      onChanged={() =>
        queryClient.invalidateQueries({ queryKey: qk.examConfigErrorTaxonomies(examTypeId) })
      }
    />
  );
}
