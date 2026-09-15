import { NextResponse } from "next/server";

/**
 * 后端在线状态探针，供 hooks/use-backend-status.ts 轮询。故意不走 /api/backend/[...path]
 * 代理（那个前缀是 /api/v1/...，而 .NET 后端的健康检查在根路径 /health，不带前缀），也故意
 * 总是 200——在线/离线都是"探测成功"，用响应体里的 `online` 字段区分，这样轮询方不需要
 * 特判网络错误 vs 业务错误。
 */
const BACKEND_BASE_URL = process.env["BACKEND_API_BASE_URL"];

export async function GET() {
  if (!BACKEND_BASE_URL) {
    return NextResponse.json({ online: false });
  }

  try {
    const res = await fetch(`${BACKEND_BASE_URL}/health`, {
      cache: "no-store",
      signal: AbortSignal.timeout(5_000),
    });
    return NextResponse.json({ online: res.ok });
  } catch {
    return NextResponse.json({ online: false });
  }
}
