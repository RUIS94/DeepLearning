import { NextRequest, NextResponse } from "next/server";
import type { EmailOtpType } from "@supabase/supabase-js";
import { getSupabaseServerClient } from "@/lib/auth/supabase-server";

/**
 * 注册确认邮件、忘记密码邮件里的链接都落地在这里——Supabase Dashboard 的邮件模板需要改成
 * `{{ .SiteURL }}/auth/confirm?token_hash={{ .TokenHash }}&type={{ .Type }}`（Auth →
 * Email Templates，Confirm signup 和 Reset password 两个模板都要改；默认模板用的
 * `{{ .ConfirmationURL }}` 指向 Supabase 自己托管的校验端点，走的是隐式流程、把 session
 * 塞在 URL fragment 里，服务端读不到，这个项目的 SSR 会话（proxy.ts 转发时要带的
 * Authorization 头、页面 generateMetadata 里读 locale 等）就会看不到登录态）。
 *
 * verifyOtp 在服务端把邮件里的一次性 token 换成真实 session，顺带把它写进 cookie
 * （getSupabaseServerClient 里的 setAll），落地页（/practice 或 /reset-password）打开时
 * 浏览器端 client 初始化就能直接从 cookie 读到这个 session，不需要再额外处理一次。
 */
export async function GET(request: NextRequest) {
  const { searchParams } = new URL(request.url);
  const tokenHash = searchParams.get("token_hash");
  const type = searchParams.get("type") as EmailOtpType | null;
  const next = searchParams.get("next") ?? (type === "recovery" ? "/reset-password" : "/practice");

  if (tokenHash && type) {
    const supabase = await getSupabaseServerClient();
    if (supabase) {
      const { error } = await supabase.auth.verifyOtp({ type, token_hash: tokenHash });
      if (!error) {
        return NextResponse.redirect(new URL(next, request.url));
      }
    }
  }

  return NextResponse.redirect(new URL("/?authError=link_invalid", request.url));
}
