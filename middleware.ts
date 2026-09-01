import { createServerClient } from "@supabase/ssr";
import { NextResponse, type NextRequest } from "next/server";

/**
 * Supabase session 刷新 + 登录态保护（方案 §4.5）。"/" 就是登录页本身（app/page.tsx → LoginPage），
 * 不是单独的 /login 路由；/api/backend/** 是代理层，不需要在这里拦截（它自己会转发未认证请求，
 * 后端 JWT 本来就是可选携带的）。
 *
 * Supabase 还没配置（NEXT_PUBLIC_SUPABASE_URL/ANON_KEY 缺失）时完全不拦截——保留原型阶段
 * "任意路径都能直接访问、无需登录"的行为，直到你提供 NEXT_PUBLIC_SUPABASE_ANON_KEY 才会真正
 * 启用登录态保护。/admin/** 目前和其他页面受同一套保护（"已登录"），design doc §16 第 4 点已
 * 点名的已知风险仍然成立：后端没有角色系统，任何登录用户都能访问 admin，不是这次改动的范围。
 */
export async function middleware(request: NextRequest) {
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

  const isLoginPage = request.nextUrl.pathname === "/";
  if (!user && !isLoginPage) {
    const loginUrl = new URL("/", request.url);
    return NextResponse.redirect(loginUrl);
  }
  // 已登录还停在登录页 → 直接进应用,避免"登录后回车/刷新又看到登录界面"。
  if (user && isLoginPage) {
    return NextResponse.redirect(new URL("/practice", request.url));
  }

  return response;
}

export const config = {
  matcher: [
    "/((?!_next/static|_next/image|api/backend|favicon.ico|.*\\.(?:svg|png|jpg|jpeg|gif|webp)$).*)",
  ],
};
