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

  // getAccessToken() 目前是桩实现，总是返回 null（Supabase Auth 还没接入，见 lib/auth/session.ts）。
  // 后端的 JWT 是可选携带的（未认证请求会 fallback 到调用方显式传的 userId，见 AGENTS.md Auth 一节），
  // 所以这不会导致任何接口打不通。
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
