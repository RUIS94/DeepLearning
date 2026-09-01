"use client";

import { PageShell } from "@/components/shell/page-shell";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { LlmProvidersPanel } from "@/app/(app)/admin/llm-providers/llm-providers-page";

export default function SettingsPage() {
  return (
    <PageShell title="设置" back backHref="/practice">
      <Tabs defaultValue="llm">
        <TabsList>
          <TabsTrigger value="llm">AI 供应商</TabsTrigger>
          <TabsTrigger value="general">通用</TabsTrigger>
        </TabsList>
        <TabsContent value="llm" className="mt-6">
          <LlmProvidersPanel />
        </TabsContent>
        <TabsContent value="general" className="mt-6">
          <p className="text-sm text-muted-foreground">通用设置占位，暂无内容。</p>
        </TabsContent>
      </Tabs>
    </PageShell>
  );
}
