"use client";

import { useQuery } from "@tanstack/react-query";
import { getSupabaseBrowserClient } from "@/lib/auth/supabase-client";
import { getUserById } from "@/lib/api/users";

/**
 * 原型阶段占位用户 id——不是 public.users 表里真实存在的一行。Supabase Auth 还没配置
 * （NEXT_PUBLIC_SUPABASE_ANON_KEY 缺失）时，各页面拿这个 id 当 userId 显式传给后端接口
 * （后端 JWT 缺失时会 fallback 到调用方传的 userId，见 AGENTS.md Auth 一节），但因为这个 id
 * 在真实数据库里没有对应行，任何需要外键关联到 users 表的写操作（创建提交、追问等）都会失败
 * ——读操作大多不受影响。真正登录一次之后，EnsureUserProfileMiddleware 会自动建出一条真实
 * users 行，到时候这里会换成用真实 session 里的用户 id。
 */
export const FALLBACK_USER_ID = "11111111-1111-4111-8111-111111111111";

export interface CurrentUser {
  id: string;
  email: string;
  displayName: string | null;
}

const FALLBACK_USER: CurrentUser = {
  id: FALLBACK_USER_ID,
  email: "learner@example.com",
  displayName: "练习者",
};

/**
 * 统一的"当前用户"读取入口（方案 §8.3 提到的 use-current-user hook）。
 * - Supabase 未配置：返回上面的占位用户，保留原型阶段"无需登录即可用"的行为。
 * - Supabase 已配置但未登录：返回 null（页面/中间件据此决定是否跳转登录页）。
 * - Supabase 已配置且已登录：调用 GET /users/{id} 拿真实 profile；
 *   万一那一步失败（例如 EnsureUserProfileMiddleware 还没来得及建档），退化成用 session 里
 *   现成的 id/email，不阻塞整个 query。
 */
export function useCurrentUser() {
  return useQuery({
    queryKey: ["current-user"],
    queryFn: async (): Promise<CurrentUser | null> => {
      const supabase = getSupabaseBrowserClient();
      if (!supabase) return FALLBACK_USER;

      const {
        data: { user },
      } = await supabase.auth.getUser();
      if (!user) return null;

      try {
        const profile = await getUserById(user.id);
        return { id: profile.id, email: profile.email, displayName: profile.displayName };
      } catch {
        return { id: user.id, email: user.email ?? "", displayName: null };
      }
    },
  });
}
