"use client";

import { useRef, useState } from "react";
import { useParams } from "next/navigation";
import { useQuery } from "@tanstack/react-query";
import { PlusCircle } from "lucide-react";
import { PageShell } from "@/components/shell/page-shell";
import { Button } from "@/components/ui/button";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import type { CrudCreateHandle } from "@/components/admin/crud-table";
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

  // 新建按钮提到 TabsList 同行；文字与动作随当前 Tab 切换。「标准修正」只增审计链、没有新建入口。
  const [tab, setTab] = useState("dimensions");
  const dimensionsCreate = useRef<CrudCreateHandle>(null);
  const taxonomiesCreate = useRef<CrudCreateHandle>(null);
  const categoriesCreate = useRef<CrudCreateHandle>(null);
  const promptCreate = useRef<CrudCreateHandle>(null);
  const createActions: Record<
    string,
    { label: string; ref: React.RefObject<CrudCreateHandle | null> }
  > = {
    dimensions: { label: "新建版本", ref: dimensionsCreate },
    "error-taxonomies": { label: "新建错误分类", ref: taxonomiesCreate },
    categories: { label: "新建题库分类", ref: categoriesCreate },
    "prompt-templates": { label: "新建 Prompt 模板", ref: promptCreate },
  };
  const activeCreate = createActions[tab];

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
        value={tab}
        onValueChange={setTab}
        className="flex min-h-0 flex-col gap-6 lg:h-full lg:overflow-hidden"
      >
        <div className="flex shrink-0 flex-wrap items-center justify-between gap-3">
          <TabsList className="flex-wrap">
            <TabsTrigger value="dimensions">评分维度</TabsTrigger>
            <TabsTrigger value="error-taxonomies">错误分类</TabsTrigger>
            <TabsTrigger value="categories">题库分类</TabsTrigger>
            <TabsTrigger value="prompt-templates">Prompt 模板</TabsTrigger>
            <TabsTrigger value="standard-overrides">标准修正</TabsTrigger>
          </TabsList>
          {activeCreate ? (
            <Button onClick={() => activeCreate.ref.current?.openCreate()}>
              <PlusCircle className="size-4" />
              {activeCreate.label}
            </Button>
          ) : null}
        </div>

        <TabsContent value="dimensions" className="mt-0 min-h-0 flex-1 lg:overflow-y-auto">
          <DimensionsPanel examTypeId={examTypeId} createRef={dimensionsCreate} />
        </TabsContent>
        <TabsContent value="error-taxonomies" className="mt-0 min-h-0 flex-1 lg:overflow-y-auto">
          <ErrorTaxonomiesPanel examTypeId={examTypeId} createRef={taxonomiesCreate} />
        </TabsContent>
        <TabsContent value="categories" className="mt-0 min-h-0 flex-1 lg:overflow-y-auto">
          <CategoriesPanel createRef={categoriesCreate} />
        </TabsContent>
        <TabsContent value="prompt-templates" className="mt-0 min-h-0 flex-1 lg:overflow-y-auto">
          <PromptTemplatesPanel createRef={promptCreate} />
        </TabsContent>
        <TabsContent value="standard-overrides" className="mt-0 min-h-0 flex-1 lg:overflow-y-auto">
          <StandardOverridesPanel />
        </TabsContent>
      </Tabs>
    </PageShell>
  );
}
