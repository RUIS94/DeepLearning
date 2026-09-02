import { createServerClient } from "@supabase/ssr";
import type { SupabaseClient } from "@supabase/supabase-js";
import { cookies } from "next/headers";

/**
 * 服务端 Supabase client（Server Component / Route Handler 用，方案 §4）。同样返回 null 而不是
 * 抛错，配合 lib/auth/session.ts 的 getAccessToken()——没配置 Supabase 时代理层照样能转发请求，
 * 只是不带 Authorization 头（后端 JWT 本来就是可选携带的）。
 */
export async function getSupabaseServerClient(): Promise<SupabaseClient | null> {
  const url = process.env["NEXT_PUBLIC_SUPABASE_URL"];
  const anonKey = process.env["NEXT_PUBLIC_SUPABASE_ANON_KEY"];
  if (!url || !anonKey) return null;

  const cookieStore = await cookies();
  return createServerClient(url, anonKey, {
    cookies: {
      getAll: () => cookieStore.getAll(),
      setAll: (cookiesToSet) => {
        try {
          cookiesToSet.forEach(({ name, value, options }) => cookieStore.set(name, value, options));
        } catch {
          // Server Component 里 cookies() 是只读的，set 会抛错——中间件（middleware.ts）已经在
          // 每次请求时负责刷新 session cookie，这里可以安全忽略，是 @supabase/ssr 官方文档里
          // 推荐的处理方式。
        }
      },
    },
  });
}
