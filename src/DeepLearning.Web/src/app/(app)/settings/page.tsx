"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { PageShell } from "@/components/shell/page-shell";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { LlmProvidersPanel } from "@/app/(app)/admin/llm-providers/llm-providers-page";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Label } from "@/components/ui/label";
import { Switch } from "@/components/ui/switch";
import { Skeleton } from "@/components/ui/skeleton";
import { showToast } from "@/components/ui/toast";
import { listFeatureFlags, setFeatureFlag } from "@/lib/api/feature-flags";
import { apiErrorMessage } from "@/lib/api/fetcher";
import { useI18n, useT, type MessageKey } from "@/lib/i18n";
import { LOCALES, LOCALE_LABELS, type Locale } from "@/lib/i18n/config";
import { qk } from "@/lib/query-keys";

function FeatureToggles() {
  const t = useT();
  const queryClient = useQueryClient();
  const flags = useQuery({ queryKey: qk.featureFlags(), queryFn: listFeatureFlags });

  const toggle = useMutation({
    mutationFn: ({ key, enabled }: { key: string; enabled: boolean }) =>
      setFeatureFlag(key, enabled),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: qk.featureFlags() });
      showToast({ title: t("settings.features.saved"), variant: "success" });
    },
    onError: (err) =>
      showToast({
        title: t("settings.features.saveFailed"),
        description: apiErrorMessage(err),
        variant: "error",
      }),
  });

  return (
    <div className="max-w-md space-y-4">
      <div>
        <h3 className="text-sm font-semibold">{t("settings.features.title")}</h3>
        <p className="text-sm text-muted-foreground">{t("settings.features.description")}</p>
      </div>

      {flags.isPending ? (
        <div className="space-y-3">
          <Skeleton className="h-12 w-full rounded-lg" />
          <Skeleton className="h-12 w-full rounded-lg" />
        </div>
      ) : (
        <div className="space-y-3">
          {(flags.data ?? []).map((flag) => (
            <div
              key={flag.key}
              className="flex items-start justify-between gap-4 rounded-lg border border-border p-3"
            >
              <div className="min-w-0">
                <p className="text-sm font-medium">
                  {t(`settings.features.flag.${flag.key}` as MessageKey)}
                </p>
                <p className="mt-0.5 text-xs text-muted-foreground">
                  {t(`settings.features.flag.${flag.key}.desc` as MessageKey)}
                </p>
              </div>
              <Switch
                checked={flag.enabled}
                disabled={toggle.isPending}
                onCheckedChange={(enabled) => toggle.mutate({ key: flag.key, enabled })}
              />
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

function GeneralSettings() {
  const t = useT();
  const { locale, setLocale } = useI18n();

  function handleChange(next: string) {
    const value = next as Locale;
    if (value === locale) return;
    setLocale(value);
    showToast({ title: t("settings.language.saved"), variant: "success" });
  }

  return (
    <div className="space-y-8">
      <div className="max-w-md space-y-2">
        <Label htmlFor="language-select">{t("settings.language.label")}</Label>
        <Select value={locale} onValueChange={handleChange}>
          <SelectTrigger id="language-select">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            {LOCALES.map((code) => (
              <SelectItem key={code} value={code}>
                {LOCALE_LABELS[code]}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
        <p className="text-sm text-muted-foreground">{t("settings.language.description")}</p>
      </div>

      <FeatureToggles />
    </div>
  );
}

export default function SettingsPage() {
  const t = useT();
  return (
    <PageShell title={t("settings.title")} back backHref="/practice">
      <Tabs defaultValue="general">
        <TabsList>
          <TabsTrigger value="general">{t("settings.tabGeneral")}</TabsTrigger>
          <TabsTrigger value="llm">{t("settings.tabProviders")}</TabsTrigger>
        </TabsList>
        <TabsContent value="general" className="mt-6">
          <GeneralSettings />
        </TabsContent>
        <TabsContent value="llm" className="mt-6">
          <LlmProvidersPanel />
        </TabsContent>
      </Tabs>
    </PageShell>
  );
}
