import type { Duration } from "@upstash/ratelimit";

/**
 * 登录/注册/忘记密码的限流策略——唯一的集中定义处，要改数字就改这里。这三个操作都是浏览器
 * 直连 Supabase Auth（见 lib/auth/supabase-client.ts），不经过任何 Next.js 路由，所以限流没法
 * 靠一个统一的代理层拦截——每个页面在真正调用 supabase.auth.* 之前，先打一次
 * POST /api/auth-guard/{action} 过一遍这里的策略（见 lib/rate-limit/limiter.ts、
 * app/api/auth-guard/[action]/route.ts）。
 *
 * AI 调用的限流是另一套、在 .NET 后端那边（Program.cs 的 AddRateLimiter）——两边分开是故意的：
 * 那些请求经 /api/backend 代理转发到后端，跟这三个"浏览器直连 Supabase"的操作完全是两条路径，
 * 硬要塞进同一层反而会把本来简单的东西搞复杂。
 */
export type AuthGuardAction = "login" | "register" | "forgotPassword";

export interface AuthGuardPolicy {
  /** 窗口期内允许的最大请求数。 */
  limit: number;
  /** Upstash duration 字符串,如 "15 m"、"1 h"。 */
  window: Duration;
  /** 仅 register 需要——还必须带一个校验通过的 Cloudflare Turnstile token。 */
  requiresTurnstile?: boolean;
}

export const AUTH_GUARD_POLICIES: Record<AuthGuardAction, AuthGuardPolicy> = {
  // 正常用户手滑输错密码几次很常见，给够余量；暴力破解才会撞到这个数字。
  login: { limit: 8, window: "15 m" },
  // 注册本身就比登录/忘记密码贵得多（Supabase 会发确认邮件），加上 Turnstile 双重把关。
  register: { limit: 5, window: "1 h", requiresTurnstile: true },
  forgotPassword: { limit: 3, window: "1 h" },
};

export function isAuthGuardAction(value: string): value is AuthGuardAction {
  return Object.hasOwn(AUTH_GUARD_POLICIES, value);
}
