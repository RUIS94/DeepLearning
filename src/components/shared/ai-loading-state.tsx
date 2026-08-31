import { AlertTriangle, Loader2, WifiOff } from "lucide-react";
import { ApiError } from "@/lib/mock/store";

export function AiLoadingState({
  status,
  error,
  pendingHint = "AI 正在处理，可能需要几秒到十几秒",
}: {
  status: "idle" | "pending" | "success" | "error";
  error?: unknown;
  pendingHint?: string;
}) {
  if (status === "pending") {
    return (
      <div className="flex items-start gap-3 rounded-lg border border-border bg-secondary/60 p-4">
        <Loader2 className="mt-0.5 size-4 shrink-0 animate-spin text-primary" />
        <div className="space-y-1">
          <p className="text-sm font-medium">{pendingHint}</p>
          <p className="text-xs text-muted-foreground">
            请勿关闭页面；后端失败时会自动重试（2s / 4s / 8s 退避）。
          </p>
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
  const apiError = error instanceof ApiError ? error : null;
  const unavailable = apiError?.status === 503;
  const Icon = unavailable ? WifiOff : AlertTriangle;

  return (
    <div className="flex items-start gap-3 rounded-lg border border-destructive/40 bg-destructive/5 p-4">
      <Icon className="mt-0.5 size-4 shrink-0 text-destructive" />
      <div className="space-y-1">
        <p className="text-sm font-medium text-destructive">
          {unavailable
            ? "AI 暂时不可用，请稍后重试"
            : (apiError?.problem?.title ??
              (error instanceof Error ? error.message : "请求失败，请稍后重试"))}
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
            错误码：{apiError.problem.correlationId}
          </p>
        ) : null}
      </div>
    </div>
  );
}
