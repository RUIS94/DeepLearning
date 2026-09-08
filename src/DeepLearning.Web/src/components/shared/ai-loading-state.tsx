"use client";

import { AlertTriangle, Loader2, WifiOff } from "lucide-react";
import { ApiError } from "@/lib/api/fetcher";
import { useT } from "@/lib/i18n";

export function AiLoadingState({
  status,
  error,
  pendingHint,
}: {
  status: "idle" | "pending" | "success" | "error";
  error?: unknown;
  pendingHint?: string;
}) {
  const t = useT();

  if (status === "pending") {
    return (
      <div className="flex items-start gap-3 rounded-lg border border-border bg-secondary/60 p-4">
        <Loader2 className="mt-0.5 size-4 shrink-0 animate-spin text-primary" />
        <div className="space-y-1">
          <p className="text-sm font-medium">{pendingHint ?? t("ai.pendingHint")}</p>
          <p className="text-xs text-muted-foreground">{t("ai.pendingDontClose")}</p>
        </div>
      </div>
    );
  }

  if (status === "error" && error) {
    return <ErrorBanner error={error} />;
  }

  return null;
}

export function ErrorBanner({ error }: { error: unknown }) {
  const t = useT();
  const apiError = error instanceof ApiError ? error : null;
  const unavailable = apiError?.status === 503;
  const Icon = unavailable ? WifiOff : AlertTriangle;

  return (
    <div className="flex items-start gap-3 rounded-lg border border-destructive/40 bg-destructive/5 p-4">
      <Icon className="mt-0.5 size-4 shrink-0 text-destructive" />
      <div className="space-y-1">
        <p className="text-sm font-medium text-destructive">
          {unavailable
            ? t("ai.unavailable")
            : (apiError?.problem?.title ??
              (error instanceof Error ? error.message : t("ai.requestFailed")))}
        </p>
        {apiError?.problem?.errors ? (
          <ul className="list-inside list-disc text-xs text-muted-foreground">
            {Object.entries(apiError.problem.errors).map(([field, messages]) => (
              <li key={field}>
                {field}：{messages.join("；")}
              </li>
            ))}
          </ul>
        ) : null}
        {apiError?.problem?.correlationId ? (
          <p className="text-numeric text-xs text-muted-foreground">
            {t("ai.errorCode")}
            {apiError.problem.correlationId}
          </p>
        ) : null}
      </div>
    </div>
  );
}
