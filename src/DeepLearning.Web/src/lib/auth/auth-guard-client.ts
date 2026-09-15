import type { AuthGuardAction } from "@/lib/rate-limit/policies";

/**
 * 登录/注册/忘记密码三个页面共用——在真正调用 supabase.auth.* 之前，先打一次
 * POST /api/auth-guard/{action}（见该路由、lib/rate-limit/*）。`turnstileToken` 只有 register
 * 会传，其它两个 action 服务端压根不会看这个字段。
 */
export type AuthGuardFailureReason =
  "rate_limited" | "turnstile_failed" | "unknown_action" | "network_error";

export type AuthGuardResult = { ok: true } | { ok: false; reason: AuthGuardFailureReason };

export async function checkAuthGuard(
  action: AuthGuardAction,
  turnstileToken?: string,
): Promise<AuthGuardResult> {
  try {
    const res = await fetch(`/api/auth-guard/${action}`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ turnstileToken }),
    });
    if (res.ok) return { ok: true };

    const body = (await res.json().catch(() => null)) as { reason?: string } | null;
    const reason: AuthGuardFailureReason =
      body?.reason === "rate_limited" || body?.reason === "turnstile_failed"
        ? body.reason
        : "unknown_action";
    return { ok: false, reason };
  } catch {
    return { ok: false, reason: "network_error" };
  }
}
