"use client";

import { createBrowserClient } from "@supabase/ssr";
import type { SupabaseClient } from "@supabase/supabase-js";

let cached: SupabaseClient | null | undefined;

/**
 * 浏览器端 Supabase client（方案 §4）。返回 null 而不是抛错——
 * NEXT_PUBLIC_SUPABASE_URL/NEXT_PUBLIC_SUPABASE_ANON_KEY 还没配置时（.env.local.example 里
 * anon key 留空），让调用方自己决定 fallback 行为（见 hooks/use-current-user.ts），而不是让
 * 整个页面崩掉——这样在真正接入 Supabase Auth 之前，应用仍然能以"未登录"的原型状态跑起来。
 */
export function getSupabaseBrowserClient(): SupabaseClient | null {
  if (cached !== undefined) return cached;
  const url = process.env["NEXT_PUBLIC_SUPABASE_URL"];
  const anonKey = process.env["NEXT_PUBLIC_SUPABASE_ANON_KEY"];
  if (!url || !anonKey) {
    cached = null;
    return null;
  }
  cached = createBrowserClient(url, anonKey);
  return cached;
}
