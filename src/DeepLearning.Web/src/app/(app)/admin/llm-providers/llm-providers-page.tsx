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
  clearAiOperationOverride,
  listAiOperationOverrides,
  listLlmProviderModels,
  listLlmProviderSettings,
  selectLlmProviderModel,
  setAiOperationOverride,
  updateLlmProviderSettings,
} from "@/lib/api/llm-providers";
import type {
  AiOperationOverrideResultItem,
  AiOperationType,
  LlmProviderSettings,
} from "@/lib/types/dtos";

const PROVIDER_LABEL: Record<string, string> = {
  claude: "Claude (Anthropic)",
  openai: "OpenAI",
  deepseek: "DeepSeek",
  mimo: "Mimo",
};

const OPERATION_TYPE_LABEL: Record<AiOperationType, string> = {
  question_gen: "生成题目",
  grading: "批改",
  followup: "追问对话（每轮）",
  standard_revision: "标准修订",
  deep_learning: "深度学习内容生成",
  progress_trend: "进度趋势总结",
  followup_summary: "追问收尾总结",
  weak_point_classification: "薄弱点分类",
  weak_point_detection_criteria: "薄弱点识别标准生成",
  weak_point_recheck: "薄弱点复查",
};

const FOLLOW_GLOBAL_VALUE = "__follow_global__";
const FOLLOW_PROVIDER_MODEL_VALUE = "__follow_provider_model__";
const THINKING_FOLLOW_PROVIDER = "__follow__";
const THINKING_ON = "on";
const THINKING_OFF = "off";

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
              {...(settings.currentModel ? { value: settings.currentModel } : {})}
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

function OperationOverrideRow({
  row,
  providerKeys,
}: {
  row: AiOperationOverrideResultItem;
  providerKeys: string[];
}) {
  const queryClient = useQueryClient();
  const invalidate = () =>
    queryClient.invalidateQueries({ queryKey: ["admin", "ai-operation-overrides"] });

  const set = useMutation({
    mutationFn: (patch: {
      providerKey: string;
      model?: string | null;
      thinkingEnabled?: boolean | null;
      effort?: string | null;
    }) =>
      setAiOperationOverride(
        row.operationType,
        patch.providerKey,
        patch.model,
        patch.thinkingEnabled,
        patch.effort,
      ),
    onSuccess: invalidate,
  });
  const clear = useMutation({
    mutationFn: () => clearAiOperationOverride(row.operationType),
    onSuccess: invalidate,
  });

  // Only fetched once a provider is pinned — with no override there is nothing to pick a model
  // or thinking flag for, since both live on the override row itself.
  const models = useQuery({
    queryKey: ["admin", "llm-provider-models", row.providerKey],
    queryFn: () => listLlmProviderModels(row.providerKey!),
    enabled: row.providerKey !== null,
  });

  const busy = set.isPending || clear.isPending;

  return (
    <div className="space-y-2 rounded-lg border border-border p-2.5">
      <div className="flex items-center justify-between gap-3">
        <span className="text-sm">{OPERATION_TYPE_LABEL[row.operationType]}</span>
        <Select
          value={row.providerKey ?? FOLLOW_GLOBAL_VALUE}
          disabled={busy}
          onValueChange={(value) =>
            value === FOLLOW_GLOBAL_VALUE
              ? clear.mutate()
              : set.mutate({ providerKey: value, model: null, thinkingEnabled: null, effort: null })
          }
        >
          <SelectTrigger className="w-56">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value={FOLLOW_GLOBAL_VALUE}>跟随全局当前供应商</SelectItem>
            {providerKeys.map((key) => (
              <SelectItem key={key} value={key}>
                {PROVIDER_LABEL[key] ?? key}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      {row.providerKey !== null ? (
        <div className="flex items-center justify-end gap-3 pl-3">
          <Select
            value={row.model ?? FOLLOW_PROVIDER_MODEL_VALUE}
            disabled={busy || models.isPending}
            onValueChange={(value) =>
              set.mutate({
                providerKey: row.providerKey!,
                model: value === FOLLOW_PROVIDER_MODEL_VALUE ? null : value,
                thinkingEnabled: row.thinkingEnabled,
                effort: row.effort,
              })
            }
          >
            <SelectTrigger className="w-56">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value={FOLLOW_PROVIDER_MODEL_VALUE}>跟随该供应商当前模型</SelectItem>
              {(models.data ?? []).map((m) => (
                <SelectItem key={m.model} value={m.model}>
                  {m.label ?? m.model}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>

          <Select
            value={
              row.thinkingEnabled === null
                ? THINKING_FOLLOW_PROVIDER
                : row.thinkingEnabled
                  ? THINKING_ON
                  : THINKING_OFF
            }
            disabled={busy}
            onValueChange={(value) =>
              set.mutate({
                providerKey: row.providerKey!,
                model: row.model,
                thinkingEnabled: value === THINKING_FOLLOW_PROVIDER ? null : value === THINKING_ON,
                effort: row.effort,
              })
            }
          >
            <SelectTrigger className="w-44">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value={THINKING_FOLLOW_PROVIDER}>Thinking：跟随供应商</SelectItem>
              <SelectItem value={THINKING_ON}>Thinking：开启</SelectItem>
              <SelectItem value={THINKING_OFF}>Thinking：关闭</SelectItem>
            </SelectContent>
          </Select>

          <Input
            key={row.effort ?? ""}
            defaultValue={row.effort ?? ""}
            placeholder="Effort：跟随供应商"
            className="w-40"
            disabled={busy}
            onBlur={(e) => {
              const value = e.target.value.trim() || null;
              if (value !== row.effort) {
                set.mutate({
                  providerKey: row.providerKey!,
                  model: row.model,
                  thinkingEnabled: row.thinkingEnabled,
                  effort: value,
                });
              }
            }}
          />
        </div>
      ) : null}

      {set.isError ? <ErrorBanner error={set.error} /> : null}
      {clear.isError ? <ErrorBanner error={clear.error} /> : null}
    </div>
  );
}

/** 按任务(AiOperationType)绑定固定 provider/model/thinking，不受全局「当前供应商」切换影响——见后端 AiOperationProviderOverride。 */
function OperationOverridesPanel({ providerKeys }: { providerKeys: string[] }) {
  const overrides = useQuery({
    queryKey: ["admin", "ai-operation-overrides"],
    queryFn: listAiOperationOverrides,
  });

  return (
    <Card className="border-border shadow-none">
      <CardHeader>
        <CardTitle className="text-base">按任务定制供应商</CardTitle>
        <p className="text-xs text-muted-foreground">
          为单个任务绑定固定的供应商、模型、thinking 开关，不受上方「设为当前供应商」切换影响。留空
          = 跟随全局当前供应商 / 该供应商自己的当前模型 / 该供应商自己的 thinking 默认值。
        </p>
      </CardHeader>
      <CardContent className="space-y-2">
        {overrides.isPending ? (
          <div className="space-y-2">
            {Array.from({ length: 10 }, (_, i) => (
              <Skeleton key={i} className="h-10 w-full" />
            ))}
          </div>
        ) : overrides.isError ? (
          <ErrorBanner error={overrides.error} />
        ) : (
          (overrides.data ?? []).map((row) => (
            <OperationOverrideRow key={row.operationType} row={row} providerKeys={providerKeys} />
          ))
        )}
      </CardContent>
    </Card>
  );
}

/** 纯内容(无页面外壳),供 /admin/llm-providers 页与 /settings 的「AI 供应商」tab 复用。 */
export function LlmProvidersPanel() {
  const settings = useQuery({
    queryKey: ["admin", "llm-provider-settings"],
    queryFn: listLlmProviderSettings,
  });

  if (settings.isPending) {
    return (
      <div className="grid gap-4 sm:grid-cols-2">
        {[0, 1, 2, 3].map((i) => (
          <Skeleton key={i} className="h-72 w-full rounded-xl" />
        ))}
      </div>
    );
  }
  if (settings.isError) {
    return <ErrorBanner error={settings.error} />;
  }
  return (
    <div className="space-y-4">
      <div className="grid gap-4 sm:grid-cols-2">
        {(settings.data ?? []).map((s) => (
          <ProviderCard key={s.providerKey} settings={s} />
        ))}
      </div>
      <OperationOverridesPanel providerKeys={(settings.data ?? []).map((s) => s.providerKey)} />
    </div>
  );
}

export function LlmProvidersPage() {
  return (
    <AdminShell
      title="AI 供应商"
      description="切换供应商/模型/thinking/effort 是数据更新，下一次 AI 调用立即生效，无需重新部署。"
    >
      <LlmProvidersPanel />
    </AdminShell>
  );
}
