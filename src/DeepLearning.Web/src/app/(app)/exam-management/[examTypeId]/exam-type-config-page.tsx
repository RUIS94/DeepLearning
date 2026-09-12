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
import { WeakPointCatalogPanel } from "@/components/exam-management/weak-point-catalog-panel";
import { useT } from "@/lib/i18n";
import { qk } from "@/lib/query-keys";
import { isAdmin, useCurrentUser } from "@/hooks/use-current-user";

export function ExamTypeConfigPage() {
  const t = useT();
  const { examTypeId } = useParams<{ examTypeId: string }>();
  const examType = useQuery({
    queryKey: qk.adminExamType(examTypeId),
    queryFn: () => getExamTypeById(examTypeId),
  });
  const { data: currentUser } = useCurrentUser();
  const admin = isAdmin(currentUser);

  // 新建按钮提到 TabsList 同行；文字与动作随当前 Tab 切换。「标准修正」只增审计链、没有新建入口。
  const [tab, setTab] = useState("dimensions");
  const dimensionsCreate = useRef<CrudCreateHandle>(null);
  const taxonomiesCreate = useRef<CrudCreateHandle>(null);
  const categoriesCreate = useRef<CrudCreateHandle>(null);
  const promptCreate = useRef<CrudCreateHandle>(null);
  const weakPointCatalogCreate = useRef<CrudCreateHandle>(null);
  // 创建动作本身是 AdminOnly 写操作(Phase 2, ref/管理员与用户权限隔离_策划书.md)——非admin
  // 完全不渲染这个 map,顶部「新建」按钮自然就没有了,不需要挨个改 5 个 panel 组件。
  const createActions: Record<
    string,
    { label: string; ref: React.RefObject<CrudCreateHandle | null> }
  > = admin
    ? {
        dimensions: { label: t("examMgmt.add.dimension"), ref: dimensionsCreate },
        "error-taxonomies": { label: t("examMgmt.add.errorTaxonomy"), ref: taxonomiesCreate },
        categories: { label: t("examMgmt.add.questionCategory"), ref: categoriesCreate },
        "prompt-templates": { label: t("examMgmt.add.promptTemplate"), ref: promptCreate },
        "weak-point-catalog": {
          label: t("examMgmt.add.weakPointCategory"),
          ref: weakPointCatalogCreate,
        },
      }
    : {};
  const activeCreate = createActions[tab];

  return (
    <PageShell
      title={
        examType.data
          ? t("examMgmt.configTitle", { name: examType.data.name })
          : t("examMgmt.configTitleFallback")
      }
      description={t("examMgmt.configDescription")}
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
            <TabsTrigger value="dimensions">{t("examMgmt.tab.dimensions")}</TabsTrigger>
            <TabsTrigger value="error-taxonomies">{t("examMgmt.tab.errorTaxonomies")}</TabsTrigger>
            <TabsTrigger value="categories">{t("examMgmt.tab.categories")}</TabsTrigger>
            {/* Prompt templates are admin-only end to end, reads included (A2, ref/管理员与用户
                权限隔离_策划书.md) — unlike the other tabs, a non-admin can't even list them, so
                the tab has to disappear entirely rather than just lose its "New" button. */}
            {admin ? (
              <TabsTrigger value="prompt-templates">
                {t("examMgmt.tab.promptTemplates")}
              </TabsTrigger>
            ) : null}
            <TabsTrigger value="weak-point-catalog">
              {t("examMgmt.tab.weakPointCatalog")}
            </TabsTrigger>
            <TabsTrigger value="standard-overrides">
              {t("examMgmt.tab.standardOverrides")}
            </TabsTrigger>
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
          <CategoriesPanel examTypeId={examTypeId} createRef={categoriesCreate} />
        </TabsContent>
        {admin ? (
          <TabsContent value="prompt-templates" className="mt-0 min-h-0 flex-1 lg:overflow-y-auto">
            <PromptTemplatesPanel examTypeId={examTypeId} createRef={promptCreate} />
          </TabsContent>
        ) : null}
        <TabsContent value="weak-point-catalog" className="mt-0 min-h-0 flex-1 lg:overflow-y-auto">
          <WeakPointCatalogPanel createRef={weakPointCatalogCreate} />
        </TabsContent>
        <TabsContent value="standard-overrides" className="mt-0 min-h-0 flex-1 lg:overflow-y-auto">
          <StandardOverridesPanel examTypeId={examTypeId} />
        </TabsContent>
      </Tabs>
    </PageShell>
  );
}
