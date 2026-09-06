"use client";

import { PageShell } from "@/components/shell/page-shell";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { LlmProvidersPanel } from "@/app/(app)/admin/llm-providers/llm-providers-page";

export default function SettingsPage() {
  return (
    <PageShell title="Settings" back backHref="/practice">
      <Tabs defaultValue="llm">
        <TabsList>
          <TabsTrigger value="llm">AI Providers</TabsTrigger>
          <TabsTrigger value="general">General</TabsTrigger>
        </TabsList>
        <TabsContent value="llm" className="mt-6">
          <LlmProvidersPanel />
        </TabsContent>
        <TabsContent value="general" className="mt-6">
          <p className="text-sm text-muted-foreground">General settings placeholder, no content available.</p>
        </TabsContent>
      </Tabs>
    </PageShell>
  );
}
