import { Ratelimit } from "@upstash/ratelimit";
import { Redis } from "@upstash/redis";
import { AUTH_GUARD_POLICIES, type AuthGuardAction } from "./policies";

/**
 * UPSTASH_REDIS_REST_URL/TOKEN 缺失时优雅降级为"不限流"，而不是让登录/注册/忘记密码直接打不
 * 通——跟这个仓库其它地方的约定一致（Supabase 未配置时也是同样的降级方式，见
 * lib/auth/supabase-client.ts）。本地开发默认没有 Upstash 账号可用。
 */
const redis =
  process.env["UPSTASH_REDIS_REST_URL"] && process.env["UPSTASH_REDIS_REST_TOKEN"]
    ? new Redis({
        url: process.env["UPSTASH_REDIS_REST_URL"],
        token: process.env["UPSTASH_REDIS_REST_TOKEN"],
      })
    : null;

// 每个 action 一个 Ratelimit 实例（各自的 limit/window），共用同一个 Redis 连接；用 prefix 隔开
// 键空间，不会互相冲突。建一次，模块级缓存——Next.js 的 Route Handler 在同一个 serverless
// 实例上会复用模块作用域,不需要每个请求都重新 new。
const limiters = new Map<AuthGuardAction, Ratelimit>();

function getLimiter(action: AuthGuardAction): Ratelimit | null {
  if (!redis) return null;

  const cached = limiters.get(action);
  if (cached) return cached;

  const policy = AUTH_GUARD_POLICIES[action];
  const limiter = new Ratelimit({
    redis,
    limiter: Ratelimit.fixedWindow(policy.limit, policy.window),
    prefix: `auth-guard:${action}`,
    analytics: true,
  });
  limiters.set(action, limiter);
  return limiter;
}

export async function checkAuthGuardRateLimit(
  action: AuthGuardAction,
  key: string,
): Promise<{ success: boolean }> {
  const limiter = getLimiter(action);
  if (!limiter) return { success: true };
  const result = await limiter.limit(key);
  return { success: result.success };
}
