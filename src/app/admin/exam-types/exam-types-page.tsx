"use client";

import { useQuery, useQueryClient } from "@tanstack/react-query";
import { AdminShell } from "@/components/shared/admin-shell";
import { CrudTable, type CrudColumn, type CrudField } from "@/components/admin/crud-table";
import { Badge } from "@/components/ui/badge";
import { createExamType, listExamTypes } from "@/lib/mock/store";
import { SubjectCategoryLabel } from "@/lib/types/enums";
import { examTypeFormSchema, type ExamTypeFormInput } from "@/lib/validation/admin";
import type { ExamType } from "@/lib/types/dtos";
import { formatDate } from "@/lib/band";

const columns: CrudColumn<ExamType>[] = [
  {
    key: "code",
    header: "Code",
    render: (e) => <span className="text-numeric font-mono text-xs">{e.code}</span>,
  },
  { key: "name", header: "名称", render: (e) => e.name },
  {
    key: "subjectCategory",
    header: "学科类别",
    render: (e) => <Badge variant="outline">{SubjectCategoryLabel[e.subjectCategory]}</Badge>,
  },
  {
    key: "languages",
    header: "语言方向",
    render: (e) =>
      e.sourceLanguage && e.targetLanguage ? `${e.sourceLanguage} → ${e.targetLanguage}` : "—",
  },
  { key: "isActive", header: "状态", render: (e) => (e.isActive ? "启用" : "停用") },
  { key: "createdAt", header: "创建时间", render: (e) => formatDate(e.createdAt) },
];

const fields: CrudField<ExamTypeFormInput>[] = [
  { name: "code", label: "Code", kind: "text", placeholder: "naati_ct_en_zh" },
  { name: "name", label: "名称", kind: "text", placeholder: "NAATI CT 英译中" },
  {
    name: "subjectCategory",
    label: "学科类别",
    kind: "select",
    valueType: "number",
    options: Object.entries(SubjectCategoryLabel).map(([v, l]) => ({ value: v, label: l })),
  },
  { name: "sourceLanguage", label: "源语言（可选）", kind: "text", placeholder: "en" },
  { name: "targetLanguage", label: "目标语言（可选）", kind: "text", placeholder: "zh" },
  { name: "gradeLevel", label: "学段（可选）", kind: "text" },
  { name: "description", label: "描述（可选）", kind: "textarea" },
];

const defaultValues: ExamTypeFormInput = {
  code: "",
  name: "",
  subjectCategory: 0,
  sourceLanguage: "",
  targetLanguage: "",
  gradeLevel: "",
  description: "",
};

export function ExamTypesPage() {
  const queryClient = useQueryClient();
  const examTypes = useQuery({ queryKey: ["admin", "exam-types"], queryFn: listExamTypes });

  return (
    <AdminShell
      title="考试类型"
      description="Rubric 即配置的骨架表。仅 Create + GetById + List，无 Update/Delete（方案 §3.7）。"
    >
      <CrudTable
        columns={columns}
        items={examTypes.data}
        isLoading={examTypes.isPending}
        loadError={examTypes.error}
        getRowId={(e) => e.id}
        schema={examTypeFormSchema}
        fields={fields}
        defaultValues={defaultValues}
        dialogTitle="新建考试类型"
        onCreate={(values) =>
          createExamType({
            ...values,
            sourceLanguage: values.sourceLanguage || null,
            targetLanguage: values.targetLanguage || null,
            gradeLevel: values.gradeLevel || null,
            description: values.description || null,
          })
        }
        onCreated={() => queryClient.invalidateQueries({ queryKey: ["admin", "exam-types"] })}
      />
    </AdminShell>
  );
}
