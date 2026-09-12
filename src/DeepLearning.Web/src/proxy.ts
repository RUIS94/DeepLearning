import { createServerClient } from "@supabase/ssr";
import { NextResponse, type NextRequest } from "next/server";

/**
 * Supabase session 刷新 + 登录态保护（方案 §4.5）。"/" 就是登录页本身（app/page.tsx → LoginPage），
 * 不是单独的 /login 路由；/api/backend/** 是代理层，不需要在这里拦截（它自己会转发未认证请求，
 * 后端 JWT 本来就是可选携带的）。
 *
 * Next.js 16 把 `middleware.ts` 约定改名为 `proxy.ts`、导出函数 `middleware` → `proxy`
 * （旧名仍可用但会告警）。行为、`config.matcher`、运行环境都不变。
 *
 * Supabase 还没配置（NEXT_PUBLIC_SUPABASE_URL/ANON_KEY 缺失）时完全不拦截——保留原型阶段
 * "任意路径都能直接访问、无需登录"的行为，直到你提供 NEXT_PUBLIC_SUPABASE_ANON_KEY 才会真正
 * 启用登录态保护。/admin/** 目前和其他页面受同一套保护（"已登录"），design doc §16 第 4 点已
 * 点名的已知风险仍然成立：后端没有角色系统，任何登录用户都能访问 admin，不是这次改动的范围。
 *
 * PUBLIC_ONLY_PATHS：未登录才用得上的页面（登录 / 注册 / 忘记密码）——已登录访问时和原来的
 * "/" 一样直接弹回应用，不需要再看一遍。
 * ALWAYS_PUBLIC_PATHS：不看登录态、无条件放行——
 *   - /auth/confirm 是注册确认邮件 / 重置密码邮件链接的落地路由（route.ts），此时请求还没有
 *     session（正是它自己要去 verifyOtp 换一个出来），如果和其它页面一样被"未登录就弹回 /"拦
 *     下，这条 Route Handler 永远不会真正执行，注册确认 / 忘记密码整个链路都会失效。
 *   - /reset-password 是上面那条路由验证通过后的下一跳，正常情况下这时已经有 session 了，
 *     但链接过期/已被用过时不会有——这里放行是为了让该页面自己展示"链接已失效"文案，而不是被
 *     这层直接静默弹回登录页。
 */
const PUBLIC_ONLY_PATHS = new Set(["/", "/register", "/forgot-password"]);
const ALWAYS_PUBLIC_PATHS = new Set(["/auth/confirm", "/reset-password"]);

export async function proxy(request: NextRequest) {
  const url = process.env["NEXT_PUBLIC_SUPABASE_URL"];
  const anonKey = process.env["NEXT_PUBLIC_SUPABASE_ANON_KEY"];
  if (!url || !anonKey) return NextResponse.next();

  let response = NextResponse.next({ request });
  const supabase = createServerClient(url, anonKey, {
    cookies: {
      getAll: () => request.cookies.getAll(),
      setAll: (cookiesToSet) => {
        cookiesToSet.forEach(({ name, value }) => request.cookies.set(name, value));
        response = NextResponse.next({ request });
        cookiesToSet.forEach(({ name, value, options }) =>
          response.cookies.set(name, value, options),
        );
      },
    },
  });

  const {
    data: { user },
  } = await supabase.auth.getUser();

  // 重定向时把 supabase 可能刚刷新过的 session cookie 一并带上,否则下一跳会丢会话。
  const redirectTo = (path: string) => {
    const res = NextResponse.redirect(new URL(path, request.url));
    response.cookies.getAll().forEach((c) => res.cookies.set(c));
    return res;
  };

  const pathname = request.nextUrl.pathname;
  const isPublicOnly = PUBLIC_ONLY_PATHS.has(pathname);
  const isAlwaysPublic = ALWAYS_PUBLIC_PATHS.has(pathname);
  if (!user && !isPublicOnly && !isAlwaysPublic) {
    return redirectTo("/");
  }
  // 已登录还停在登录/注册/忘记密码页 → 直接进应用,避免"登录后回车/刷新又看到登录界面"。
  if (user && isPublicOnly) {
    return redirectTo("/practice");
  }

  return response;
}

export const config = {
  matcher: [
    "/((?!_next/static|_next/image|api/backend|favicon.ico|.*\\.(?:svg|png|jpg|jpeg|gif|webp)$).*)",
  ],
};
