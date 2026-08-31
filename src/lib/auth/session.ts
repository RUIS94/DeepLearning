/**
 * 会话/token 读取——目前是桩实现，Supabase Auth 还没接入（方案 §4，进度跟踪.md 里明确标为
 * "暂缓，等后端联调时再做"）。代理层（app/api/backend/[...path]/route.ts）从这里取 token 加到
 * Authorization 头上；后端本身对没有 token 的请求也不会拒绝（JWT 是可选携带，见 AGENTS.md
 * Auth 一节与方案 §3.10），所以现在返回 null 不会导致任何接口打不通，只是暂时无法以真实用户身份调用。
 *
 * 接 Supabase 时，把这里换成：
 *   import { createServerClient } from "@supabase/ssr";
 *   const supabase = createServerClient(url, anonKey, { cookies });
 *   const { data: { session } } = await supabase.auth.getSession();
 *   return session?.access_token ?? null;
 * 调用方（proxy route handler）不需要跟着改。
 */
export async function getAccessToken(): Promise<string | null> {
  return null;
}
