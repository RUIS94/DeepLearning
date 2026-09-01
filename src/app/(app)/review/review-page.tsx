"use client";

import { PageShell } from "@/components/shell/page-shell";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { ReviewLibraryPanel } from "@/components/review/review-library-panel";
import { WeakPointsPanel } from "@/components/review/weak-points-panel";

export function ReviewPage() {
  return (
    <PageShell title="复习" description="练习中沉淀的句型/词汇复习库，以及 AI 归类的薄弱点。">
      <Tabs defaultValue="library">
        <TabsList>
          <TabsTrigger value="library">复习库</TabsTrigger>
          <TabsTrigger value="weak-points">薄弱点</TabsTrigger>
        </TabsList>
        <TabsContent value="library" className="mt-6">
          <ReviewLibraryPanel />
        </TabsContent>
        <TabsContent value="weak-points" className="mt-6">
          <WeakPointsPanel />
        </TabsContent>
      </Tabs>
    </PageShell>
  );
}
