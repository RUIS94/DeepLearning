import { NextRequest, NextResponse } from "next/server";
import { getAccessToken } from "@/lib/auth/session";

/**
 * 同源代理层（方案 §5.1/5.2）。存在的唯一原因：后端没有配置 CORS（AGENTS.md、Program.cs 都确认
 * 过这一点），浏览器发起的跨源请求会被直接拦下。Client Component 里的所有 mutation/fetch 都应该
 * 打 `/api/backend/...`，不要直接打 BACKEND_API_BASE_URL——Server Component 的只读请求则相反，
 * 应该服务端到服务端直连（见 lib/api/fetcher.ts 的 createServerApiClient），不需要也不应该走这里。
 *
 * BACKEND_API_BASE_URL 不加 NEXT_PUBLIC_ 前缀，只在服务端可见，浏览器拿不到后端真实地址。
 */

const BACKEND_BASE_URL = process.env["BACKEND_API_BASE_URL"];

async function proxy(req: NextRequest, path: string[]): Promise<NextResponse> {
  if (!BACKEND_BASE_URL) {
    return NextResponse.json(
      { status: 500, title: "BACKEND_API_BASE_URL is not set on the server." },
      { status: 500 },
    );
  }

  // getAccessToken() 从 @supabase/ssr 的服务端 client 取当前会话的 access token（lib/auth/session.ts，
  // 已是真实实现）。这条 Route Handler 不在 proxy.ts 的 matcher 里（matcher 排除了 /api/backend），
  // 所以这里不会有中间件的会话刷新——但 getSession() 在 Route Handler 里遇到过期 token 会自行刷新并
  // 写回 cookie。Supabase 未配置或未登录时返回 null；后端 JWT 可选携带，未认证请求 fallback 到调用方
  // 显式传的 userId（见 AGENTS.md Auth 一节），所以缺 token 不会让接口打不通，只是不以真实用户身份调用。
  const token = await getAccessToken();
  const targetUrl = `${BACKEND_BASE_URL}/api/v1/${path.join("/")}${req.nextUrl.search}`;

  const headers = new Headers();
  const incomingContentType = req.headers.get("content-type");
  headers.set("Content-Type", incomingContentType ?? "application/json");
  if (token) headers.set("Authorization", `Bearer ${token}`);
  // 透传 correlation id，方便前后端日志对上（CorrelationIdMiddleware 会在响应里回传同一个值）。
  const incomingCorrelationId = req.headers.get("x-correlation-id");
  if (incomingCorrelationId) headers.set("X-Correlation-Id", incomingCorrelationId);

  const hasBody = !["GET", "HEAD"].includes(req.method);
  const upstream = await fetch(targetUrl, {
    method: req.method,
    headers,
    body: hasBody ? await req.text() : null,
  });

  const responseHeaders = new Headers();
  const responseContentType = upstream.headers.get("content-type");
  if (responseContentType) responseHeaders.set("Content-Type", responseContentType);
  const correlationId = upstream.headers.get("x-correlation-id");
  if (correlationId) responseHeaders.set("X-Correlation-Id", correlationId);

  return new NextResponse(upstream.body, { status: upstream.status, headers: responseHeaders });
}

type RouteContext = { params: Promise<{ path: string[] }> };

export async function GET(req: NextRequest, { params }: RouteContext) {
  return proxy(req, (await params).path);
}
export async function POST(req: NextRequest, { params }: RouteContext) {
  return proxy(req, (await params).path);
}
export async function PUT(req: NextRequest, { params }: RouteContext) {
  return proxy(req, (await params).path);
}
export async function PATCH(req: NextRequest, { params }: RouteContext) {
  return proxy(req, (await params).path);
}
export async function DELETE(req: NextRequest, { params }: RouteContext) {
  return proxy(req, (await params).path);
}
