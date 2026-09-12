"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Switch } from "@/components/ui/switch";
import { Skeleton } from "@/components/ui/skeleton";
import { showToast } from "@/components/ui/toast";
import { listFeatureFlags, setFeatureFlag } from "@/lib/api/feature-flags";
import { apiErrorMessage } from "@/lib/api/fetcher";
import { useT, type MessageKey } from "@/lib/i18n";
import { qk } from "@/lib/query-keys";

/**
 * Global feature_flags toggles — admin-only (D2'/A1/A4, ref/管理员与用户权限隔离_策划书.md).
 * Relocated here from the Settings page, which every logged-in user could reach; the backend's
 * GET/PUT /feature-flags is AdminOnly now, so it can no longer live anywhere a non-admin visits.
 * Per-user overrides (which override a single user's access independent of this global value)
 * live on each row in AdminUsersPage instead.
 */
export function GlobalFeatureToggles() {
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
