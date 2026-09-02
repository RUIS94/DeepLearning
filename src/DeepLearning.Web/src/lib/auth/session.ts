import { getSupabaseServerClient } from "./supabase-server";

/**
 * 代理层（app/api/backend/[...path]/route.ts）从这里取 token 加到 Authorization 头上。
 * Supabase 还没配置（NEXT_PUBLIC_SUPABASE_URL/ANON_KEY 缺失）时返回 null——后端的 JWT 是可选
 * 携带的（未认证请求会 fallback 到调用方显式传的 userId，见 AGENTS.md Auth 一节与方案 §3.10），
 * 所以这不会导致任何接口打不通，只是暂时无法以真实登录用户的身份调用。
 */
export async function getAccessToken(): Promise<string | null> {
  const supabase = await getSupabaseServerClient();
  if (!supabase) return null;
  const {
    data: { session },
  } = await supabase.auth.getSession();
  return session?.access_token ?? null;
}
