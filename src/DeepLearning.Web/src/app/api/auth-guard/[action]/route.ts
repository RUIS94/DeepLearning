import { NextResponse, type NextRequest } from "next/server";
import { AUTH_GUARD_POLICIES, isAuthGuardAction } from "@/lib/rate-limit/policies";
import { checkAuthGuardRateLimit } from "@/lib/rate-limit/limiter";
import { verifyTurnstileToken } from "@/lib/turnstile";

/**
 * 登录/注册/忘记密码在真正调用 supabase.auth.* 之前先过这一关（见三个页面组件里的
 * fetch("/api/auth-guard/...")）。POST /api/auth-guard/login|register|forgotPassword。
 *
 * 不在 proxy.ts 的登录态拦截范围内——必须匿名可访问，这本来就是给"还没登录的人"挡门的。
 */

function clientIp(req: NextRequest): string {
  const forwarded = req.headers.get("x-forwarded-for");
  if (forwarded) return forwarded.split(",")[0]!.trim();
  return req.headers.get("x-real-ip") ?? "unknown";
}

type RouteContext = { params: Promise<{ action: string }> };

export async function POST(req: NextRequest, { params }: RouteContext) {
  const { action } = await params;
  if (!isAuthGuardAction(action)) {
    return NextResponse.json({ ok: false, reason: "unknown_action" }, { status: 404 });
  }

  const policy = AUTH_GUARD_POLICIES[action];
  const ip = clientIp(req);

  if (policy.requiresTurnstile) {
    const body = (await req.json().catch(() => null)) as { turnstileToken?: string } | null;
    const verified = await verifyTurnstileToken(body?.turnstileToken, ip);
    if (!verified) {
      return NextResponse.json({ ok: false, reason: "turnstile_failed" }, { status: 400 });
    }
  }

  const { success } = await checkAuthGuardRateLimit(action, ip);
  if (!success) {
    return NextResponse.json({ ok: false, reason: "rate_limited" }, { status: 429 });
  }

  return NextResponse.json({ ok: true });
}
