"use client";

import { useParams } from "next/navigation";
import { useQuery } from "@tanstack/react-query";
import { PageShell } from "@/components/shell/page-shell";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { getExamTypeById } from "@/lib/api/exam-config";
import { DimensionsPanel } from "@/components/exam-management/dimensions-panel";
import { ErrorTaxonomiesPanel } from "@/components/exam-management/error-taxonomies-panel";
import { CategoriesPanel } from "@/components/exam-management/categories-panel";
import { PromptTemplatesPanel } from "@/components/exam-management/prompt-templates-panel";
import { StandardOverridesPanel } from "@/components/exam-management/standard-overrides-panel";

export function ExamTypeConfigPage() {
  const { examTypeId } = useParams<{ examTypeId: string }>();
  const examType = useQuery({
    queryKey: ["admin", "exam-type", examTypeId],
    queryFn: () => getExamTypeById(examTypeId),
  });

  return (
    <PageShell
      title={examType.data ? `配置 · ${examType.data.name}` : "考试配置"}
      description="评分维度、错误分类、题库分类、Prompt 模板、标准修正。"
      back
      backHref="/exam-management"
    >
      {/* lg 及以上：整页锁视口，TabsList 固定不滚动，只有当前 Tab 的内容区滚动
          （见 AGENTS.md 的 full-height 分层规则）；lg 以下沿用 PageShell body 的整页滚动。 */}
      <Tabs
        defaultValue="dimensions"
        className="flex min-h-0 flex-col gap-6 lg:h-full lg:overflow-hidden"
      >
        <TabsList className="shrink-0 self-start flex-wrap">
          <TabsTrigger value="dimensions">评分维度</TabsTrigger>
          <TabsTrigger value="error-taxonomies">错误分类</TabsTrigger>
          <TabsTrigger value="categories">题库分类</TabsTrigger>
          <TabsTrigger value="prompt-templates">Prompt 模板</TabsTrigger>
          <TabsTrigger value="standard-overrides">标准修正</TabsTrigger>
        </TabsList>

        <TabsContent value="dimensions" className="mt-0 min-h-0 flex-1 lg:overflow-y-auto">
          <DimensionsPanel examTypeId={examTypeId} />
        </TabsContent>
        <TabsContent value="error-taxonomies" className="mt-0 min-h-0 flex-1 lg:overflow-y-auto">
          <ErrorTaxonomiesPanel examTypeId={examTypeId} />
        </TabsContent>
        <TabsContent value="categories" className="mt-0 min-h-0 flex-1 lg:overflow-y-auto">
          <CategoriesPanel />
        </TabsContent>
        <TabsContent value="prompt-templates" className="mt-0 min-h-0 flex-1 lg:overflow-y-auto">
          <PromptTemplatesPanel />
        </TabsContent>
        <TabsContent value="standard-overrides" className="mt-0 min-h-0 flex-1 lg:overflow-y-auto">
          <StandardOverridesPanel />
        </TabsContent>
      </Tabs>
    </PageShell>
  );
}
