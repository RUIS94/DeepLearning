"use client";

import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { CheckCircle2, PlusCircle } from "lucide-react";
import { AdminShell } from "@/components/shared/admin-shell";
import { ErrorBanner } from "@/components/shared/ai-loading-state";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Switch } from "@/components/ui/switch";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Skeleton } from "@/components/ui/skeleton";
import {
  activateLlmProvider,
  addLlmProviderModel,
  listLlmProviderModels,
  listLlmProviderSettings,
  selectLlmProviderModel,
  updateLlmProviderSettings,
} from "@/lib/mock/store";
import type { LlmProviderSettings } from "@/lib/types/dtos";

const PROVIDER_LABEL: Record<string, string> = {
  claude: "Claude (Anthropic)",
  openai: "OpenAI",
  deepseek: "DeepSeek",
  mimo: "Mimo",
};

function ProviderCard({ settings }: { settings: LlmProviderSettings }) {
  const queryClient = useQueryClient();
  const [newModel, setNewModel] = useState("");

  const models = useQuery({
    queryKey: ["admin", "llm-provider-models", settings.providerKey],
    queryFn: () => listLlmProviderModels(settings.providerKey),
  });

  const invalidateAll = () => {
    queryClient.invalidateQueries({ queryKey: ["admin", "llm-provider-settings"] });
    queryClient.invalidateQueries({
      queryKey: ["admin", "llm-provider-models", settings.providerKey],
    });
  };

  const activate = useMutation({
    mutationFn: () => activateLlmProvider(settings.providerKey),
    onSuccess: invalidateAll,
  });
  const updateSettings = useMutation({
    mutationFn: (patch: Parameters<typeof updateLlmProviderSettings>[1]) =>
      updateLlmProviderSettings(settings.providerKey, patch),
    onSuccess: invalidateAll,
  });
  const selectModel = useMutation({
    mutationFn: (model: string) => selectLlmProviderModel(settings.providerKey, model),
    onSuccess: invalidateAll,
  });
  const addModel = useMutation({
    mutationFn: (model: string) => addLlmProviderModel(settings.providerKey, model),
    onSuccess: () => {
      setNewModel("");
      invalidateAll();
    },
  });

  return (
    <Card className="border-border shadow-none">
      <CardHeader className="flex-row items-center justify-between space-y-0">
        <CardTitle className="flex items-center gap-2 text-base">
          {PROVIDER_LABEL[settings.providerKey] ?? settings.providerKey}
          {settings.isActive ? (
            <Badge variant="outline" className="border-transparent bg-success/12 text-success">
              <CheckCircle2 className="size-3.5" />
              当前使用中
            </Badge>
          ) : null}
        </CardTitle>
        {!settings.isActive ? (
          <Button
            size="sm"
            variant="outline"
            disabled={activate.isPending}
            onClick={() => activate.mutate()}
          >
            设为当前供应商
          </Button>
        ) : null}
      </CardHeader>
      <CardContent className="space-y-4">
        <div className="flex items-center justify-between rounded-lg border border-border p-3">
          <div>
            <p className="text-sm font-medium">Thinking / 扩展推理</p>
            <p className="text-xs text-muted-foreground">
              目前仅 Claude 语义完整支持（见 AGENTS.md）。
            </p>
          </div>
          <Switch
            checked={settings.thinkingEnabled}
            disabled={updateSettings.isPending}
            onCheckedChange={(checked) => updateSettings.mutate({ thinkingEnabled: checked })}
          />
        </div>

        <div className="space-y-2">
          <Label>Effort</Label>
          <Input
            defaultValue={settings.effort ?? ""}
            placeholder="low / medium / high"
            onBlur={(e) => {
              if (e.target.value !== (settings.effort ?? "")) {
                updateSettings.mutate({ effort: e.target.value || null });
              }
            }}
          />
        </div>

        <div className="space-y-2">
          <Label>当前模型</Label>
          {models.isPending ? (
            <Skeleton className="h-9 w-full" />
          ) : (
            <Select
              {...(settings.currentModel?.model ? { value: settings.currentModel.model } : {})}
              onValueChange={(model) => selectModel.mutate(model)}
            >
              <SelectTrigger>
                <SelectValue placeholder="尚未设置当前模型" />
              </SelectTrigger>
              <SelectContent>
                {(models.data ?? []).map((m) => (
                  <SelectItem key={m.model} value={m.model}>
                    {m.label ?? m.model}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          )}
        </div>

        <div className="space-y-2">
          <Label className="text-xs text-muted-foreground">添加新模型到目录</Label>
          <div className="flex gap-2">
            <Input
              value={newModel}
              onChange={(e) => setNewModel(e.target.value)}
              placeholder="例如 claude-opus-5-2"
            />
            <Button
              size="icon"
              variant="outline"
              disabled={!newModel.trim() || addModel.isPending}
              onClick={() => addModel.mutate(newModel.trim())}
            >
              <PlusCircle className="size-4" />
            </Button>
          </div>
          {addModel.isError ? <ErrorBanner error={addModel.error} /> : null}
        </div>
      </CardContent>
    </Card>
  );
}

export function LlmProvidersPage() {
  const settings = useQuery({
    queryKey: ["admin", "llm-provider-settings"],
    queryFn: listLlmProviderSettings,
  });

  return (
    <AdminShell
      title="AI 供应商"
      description="切换供应商/模型/thinking/effort 是数据更新，下一次 AI 调用立即生效，无需重新部署。"
    >
      {settings.isPending ? (
        <div className="grid gap-4 sm:grid-cols-2">
          {[0, 1, 2, 3].map((i) => (
            <Skeleton key={i} className="h-72 w-full rounded-xl" />
          ))}
        </div>
      ) : settings.isError ? (
        <ErrorBanner error={settings.error} />
      ) : (
        <div className="grid gap-4 sm:grid-cols-2">
          {(settings.data ?? []).map((s) => (
            <ProviderCard key={s.providerKey} settings={s} />
          ))}
        </div>
      )}
    </AdminShell>
  );
}
