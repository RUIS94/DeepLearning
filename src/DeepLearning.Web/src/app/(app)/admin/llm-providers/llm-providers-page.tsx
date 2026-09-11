"use client";

import { useState, type ReactNode } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { CheckCircle2, PlusCircle } from "lucide-react";
import { AppShell } from "@/components/shared/app-shell";
import { ErrorBanner } from "@/components/shared/ai-loading-state";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Switch } from "@/components/ui/switch";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from "@/components/ui/dialog";
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
import { useT, type TranslateFn } from "@/lib/i18n";
import { cn } from "@/lib/utils";

const PROVIDER_LABEL: Record<string, string> = {
  claude: "Claude (Anthropic)",
  openai: "OpenAI",
  deepseek: "DeepSeek",
  mimo: "Mimo",
};

const opTypeLabel = (t: TranslateFn, op: AiOperationType): string =>
  t(`llm.op.${op}` as "llm.op.grading");

const FOLLOW_GLOBAL_VALUE = "__follow_global__";
const FOLLOW_PROVIDER_MODEL_VALUE = "__follow_provider_model__";
const THINKING_FOLLOW_PROVIDER = "__follow__";
const EFFORT_AUTO = "__auto__";
const EFFORT_OPTIONS = ["low", "medium", "high"] as const;
const THINKING_ON = "on";
const THINKING_OFF = "off";

/** 任务名列自适应，其余 4 列固定等宽——表头和每一行都用这套模板，列自然对齐。 */
const OVERRIDE_ROW_GRID =
  "grid grid-cols-[minmax(140px,1fr)_9rem_9rem_9rem_9rem] items-center gap-2";

/** 未固定供应商时，model/thinking/effort 三列没有实际含义，用一个禁用的占位格显示
 * 「跟随全局」——和供应商列此时的值一致，让整行读起来是「全部跟随全局」，且不破坏等宽的列对齐。 */
function FollowGlobalCell({ label }: { label: string }) {
  return (
    <div className="flex h-8 w-full items-center rounded-md border border-input bg-transparent px-3 text-xs text-muted-foreground/70">
      {label}
    </div>
  );
}

/** 一行「标签 + 控件」，控件靠右、宽度收紧，不再包 border 盒子。 */
function SettingRow({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="flex items-center justify-between gap-3 py-1.5">
      <span className="text-sm text-muted-foreground">{label}</span>
      {children}
    </div>
  );
}

function ProviderCard({ settings }: { settings: LlmProviderSettings }) {
  const t = useT();
  const queryClient = useQueryClient();

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

  return (
    <Card className="shadow-none">
      <CardHeader className="flex-row items-center justify-between gap-2 space-y-0 pb-3">
        <CardTitle className="text-sm">
          {PROVIDER_LABEL[settings.providerKey] ?? settings.providerKey}
        </CardTitle>
        {settings.isActive ? (
          <Badge variant="outline" className="gap-1 border-transparent bg-success/12 text-success">
            <CheckCircle2 className="size-3.5" />
            {t("llm.inUse")}
          </Badge>
        ) : (
          <Button
            size="sm"
            variant="ghost"
            className="h-7 bg-accent px-3 text-xs text-accent-foreground hover:bg-accent/80"
            disabled={activate.isPending}
            onClick={() => activate.mutate()}
          >
            {t("llm.setAsCurrent")}
          </Button>
        )}
      </CardHeader>
      <CardContent className="pb-4">
        <div className="grid grid-cols-2 gap-3">
          <div className="space-y-1.5">
            <Label className="text-xs text-muted-foreground">{t("llm.currentModel")}</Label>
            {models.isPending ? (
              <Skeleton className="h-8 w-full" />
            ) : (
              <Select
                {...(settings.currentModel ? { value: settings.currentModel } : {})}
                onValueChange={(model) => selectModel.mutate(model)}
              >
                <SelectTrigger className="h-8 text-sm">
                  <SelectValue placeholder={t("llm.noCurrentModel")} />
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

          <div className="space-y-1.5">
            <Label className="text-xs text-muted-foreground">Effort</Label>
            <Select
              value={settings.effort ?? EFFORT_AUTO}
              disabled={updateSettings.isPending}
              onValueChange={(value) =>
                updateSettings.mutate({ effort: value === EFFORT_AUTO ? null : value })
              }
            >
              <SelectTrigger className="h-8 text-sm">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value={EFFORT_AUTO}>{t("llm.effortDefault")}</SelectItem>
                {EFFORT_OPTIONS.map((level) => (
                  <SelectItem key={level} value={level}>
                    {level}
                  </SelectItem>
                ))}
                {settings.effort && !EFFORT_OPTIONS.includes(settings.effort as never) ? (
                  <SelectItem value={settings.effort}>{settings.effort}</SelectItem>
                ) : null}
              </SelectContent>
            </Select>
          </div>
        </div>

        <div className="mt-3">
          <SettingRow label={t("llm.thinkingLabel")}>
            <Switch
              checked={settings.thinkingEnabled}
              disabled={updateSettings.isPending}
              onCheckedChange={(checked) => updateSettings.mutate({ thinkingEnabled: checked })}
            />
          </SettingRow>
        </div>
      </CardContent>
    </Card>
  );
}

/** 「先选供应商 → 输入 model id → 保存」的独立弹窗，取代原先每张卡片里的添加行。 */
function AddModelDialog({ providerKeys }: { providerKeys: string[] }) {
  const t = useT();
  const queryClient = useQueryClient();
  const [open, setOpen] = useState(false);
  const [providerKey, setProviderKey] = useState(providerKeys[0] ?? "");
  const [model, setModel] = useState("");
  const [label, setLabel] = useState("");

  const addModel = useMutation({
    mutationFn: () => addLlmProviderModel(providerKey, model.trim(), label.trim() || null),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["admin", "llm-provider-settings"] });
      queryClient.invalidateQueries({
        queryKey: ["admin", "llm-provider-models", providerKey],
      });
      setModel("");
      setLabel("");
      setOpen(false);
    },
  });

  return (
    <Dialog
      open={open}
      onOpenChange={(next) => {
        setOpen(next);
        if (!next) {
          setModel("");
          setLabel("");
          addModel.reset();
        }
      }}
    >
      <DialogTrigger asChild>
        <Button size="sm" variant="secondary" className="gap-1.5">
          <PlusCircle className="size-4" />
          {t("llm.addModelTitle")}
        </Button>
      </DialogTrigger>
      <DialogContent className="max-w-sm">
        <DialogHeader>
          <DialogTitle>{t("llm.addModelTitle")}</DialogTitle>
          <DialogDescription>{t("llm.addModelDialogHint")}</DialogDescription>
        </DialogHeader>
        <div className="space-y-3">
          <div className="space-y-1.5">
            <Label>{t("llm.fieldProvider")}</Label>
            <Select value={providerKey} onValueChange={setProviderKey}>
              <SelectTrigger>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {providerKeys.map((key) => (
                  <SelectItem key={key} value={key}>
                    {PROVIDER_LABEL[key] ?? key}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-1.5">
            <Label>{t("llm.fieldModelId")}</Label>
            <Input
              autoFocus
              value={model}
              onChange={(e) => setModel(e.target.value)}
              placeholder={t("llm.addModelPh")}
            />
          </div>
          <div className="space-y-1.5">
            <Label>{t("llm.fieldLabelOptional")}</Label>
            <Input
              value={label}
              onChange={(e) => setLabel(e.target.value)}
              placeholder={t("llm.addModelLabelPh")}
            />
          </div>
          {addModel.isError ? <ErrorBanner error={addModel.error} /> : null}
        </div>
        <DialogFooter>
          <Button variant="ghost" onClick={() => setOpen(false)}>
            {t("common.cancel")}
          </Button>
          <Button
            disabled={!providerKey || !model.trim() || addModel.isPending}
            onClick={() => addModel.mutate()}
          >
            {t("common.save")}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

function OperationOverrideRow({
  row,
  providerKeys,
}: {
  row: AiOperationOverrideResultItem;
  providerKeys: string[];
}) {
  const t = useT();
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
    <div className="py-2">
      <div className={OVERRIDE_ROW_GRID}>
        <span className="min-w-0 truncate text-sm" title={opTypeLabel(t, row.operationType)}>
          {opTypeLabel(t, row.operationType)}
        </span>

        <Select
          value={row.providerKey ?? FOLLOW_GLOBAL_VALUE}
          disabled={busy}
          onValueChange={(value) =>
            value === FOLLOW_GLOBAL_VALUE
              ? clear.mutate()
              : set.mutate({
                  providerKey: value,
                  model: null,
                  thinkingEnabled: null,
                  effort: null,
                })
          }
        >
          <SelectTrigger className="h-8 w-full text-xs">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value={FOLLOW_GLOBAL_VALUE}>{t("llm.followGlobalShort")}</SelectItem>
            {providerKeys.map((key) => (
              <SelectItem key={key} value={key}>
                {PROVIDER_LABEL[key] ?? key}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>

        {row.providerKey !== null ? (
          <>
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
              <SelectTrigger className="h-8 w-full text-xs">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value={FOLLOW_PROVIDER_MODEL_VALUE}>
                  {t("llm.providerDefault")}
                </SelectItem>
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
                  thinkingEnabled:
                    value === THINKING_FOLLOW_PROVIDER ? null : value === THINKING_ON,
                  effort: row.effort,
                })
              }
            >
              <SelectTrigger className="h-8 w-full text-xs">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value={THINKING_FOLLOW_PROVIDER}>{t("llm.providerDefault")}</SelectItem>
                <SelectItem value={THINKING_ON}>{t("llm.thinkingOnShort")}</SelectItem>
                <SelectItem value={THINKING_OFF}>{t("llm.thinkingOffShort")}</SelectItem>
              </SelectContent>
            </Select>

            <Select
              value={row.effort ?? EFFORT_AUTO}
              disabled={busy}
              onValueChange={(value) =>
                set.mutate({
                  providerKey: row.providerKey!,
                  model: row.model,
                  thinkingEnabled: row.thinkingEnabled,
                  effort: value === EFFORT_AUTO ? null : value,
                })
              }
            >
              <SelectTrigger className="h-8 w-full text-xs">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value={EFFORT_AUTO}>{t("llm.providerDefault")}</SelectItem>
                {EFFORT_OPTIONS.map((level) => (
                  <SelectItem key={level} value={level}>
                    {level}
                  </SelectItem>
                ))}
                {row.effort && !EFFORT_OPTIONS.includes(row.effort as never) ? (
                  <SelectItem value={row.effort}>{row.effort}</SelectItem>
                ) : null}
              </SelectContent>
            </Select>
          </>
        ) : (
          // 未固定供应商——model/thinking/effort 都跟随全局，用同样的文案显示，保持列对齐。
          <>
            <FollowGlobalCell label={t("llm.followGlobalShort")} />
            <FollowGlobalCell label={t("llm.followGlobalShort")} />
            <FollowGlobalCell label={t("llm.followGlobalShort")} />
          </>
        )}
      </div>

      {set.isError ? (
        <div className="mt-1">
          <ErrorBanner error={set.error} />
        </div>
      ) : null}
      {clear.isError ? (
        <div className="mt-1">
          <ErrorBanner error={clear.error} />
        </div>
      ) : null}
    </div>
  );
}

/** 按任务(AiOperationType)绑定固定 provider/model/thinking，不受全局「当前供应商」切换影响——见后端 AiOperationProviderOverride。 */
function OperationOverridesPanel({ providerKeys }: { providerKeys: string[] }) {
  const t = useT();
  const overrides = useQuery({
    queryKey: ["admin", "ai-operation-overrides"],
    queryFn: listAiOperationOverrides,
  });

  return (
    <Card className="shadow-none">
      <CardHeader className="pb-2">
        <CardTitle className="text-sm">{t("llm.perTaskTitle")}</CardTitle>
        <p className="text-xs text-muted-foreground">{t("llm.perTaskHint")}</p>
      </CardHeader>
      <CardContent>
        {overrides.isPending ? (
          <div className="space-y-2">
            {Array.from({ length: 10 }, (_, i) => (
              <Skeleton key={i} className="h-9 w-full" />
            ))}
          </div>
        ) : overrides.isError ? (
          <ErrorBanner error={overrides.error} />
        ) : (
          <div className="-mx-1 overflow-x-auto px-1 py-1">
            <div className="min-w-[760px] pr-1">
              <div
                className={cn(
                  OVERRIDE_ROW_GRID,
                  "pb-1.5 text-[11px] font-medium uppercase tracking-wide text-muted-foreground/70",
                )}
              >
                <span />
                <span>{t("llm.fieldProvider")}</span>
                <span>{t("llm.fieldModel")}</span>
                <span>{t("llm.thinkingShort")}</span>
                <span>{t("llm.effortShort")}</span>
              </div>
              <div className="divide-y divide-border/60">
                {(overrides.data ?? []).map((row) => (
                  <OperationOverrideRow
                    key={row.operationType}
                    row={row}
                    providerKeys={providerKeys}
                  />
                ))}
              </div>
            </div>
          </div>
        )}
      </CardContent>
    </Card>
  );
}

/** 纯内容(无页面外壳),供 /admin/llm-providers 页与 /settings 的「AI 供应商」tab 复用。 */
export function LlmProvidersPanel() {
  const t = useT();
  const settings = useQuery({
    queryKey: ["admin", "llm-provider-settings"],
    queryFn: listLlmProviderSettings,
  });

  if (settings.isPending) {
    return (
      <div className="grid gap-4 sm:grid-cols-2">
        {[0, 1, 2, 3].map((i) => (
          <Skeleton key={i} className="h-44 w-full rounded-xl" />
        ))}
      </div>
    );
  }
  if (settings.isError) {
    return <ErrorBanner error={settings.error} />;
  }

  const providerKeys = (settings.data ?? []).map((s) => s.providerKey);

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between gap-3">
        <p className="text-xs text-muted-foreground">{t("llm.description")}</p>
        <AddModelDialog providerKeys={providerKeys} />
      </div>
      <div className="grid gap-4 sm:grid-cols-2">
        {(settings.data ?? []).map((s) => (
          <ProviderCard key={s.providerKey} settings={s} />
        ))}
      </div>
      <OperationOverridesPanel providerKeys={providerKeys} />
    </div>
  );
}

export function LlmProvidersPage() {
  const t = useT();
  return (
    <AppShell title={t("llm.title")} description={t("llm.description")}>
      <LlmProvidersPanel />
    </AppShell>
  );
}
