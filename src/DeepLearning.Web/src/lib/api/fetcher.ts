import type { ProblemDetails } from "@/lib/types/dtos";

/**
 * 统一 fetcher（方案 §5.3）。`lib/api/*.ts` 资源模块统一用 `api("/questions")` 这种不带
 * `/api/v1` 前缀的资源路径调用——两种 client 都在各自的 baseUrl 里把 `/api/v1` 前缀补好：
 * 代理层 `route.ts` 转发时自己拼 `/api/v1/`（浏览器请求走 `/api/backend/...`，同源，无 CORS 问题）；
 * `createServerApiClient` 的 baseUrl 直接是 `${BACKEND_API_BASE_URL}/api/v1`（Server Component
 * 服务端到服务端直连，不经代理，发起方不是浏览器所以没有 CORS 问题，见方案 §5.1）。
 */

export type FetchOptions = {
  method?: "GET" | "POST" | "PUT" | "PATCH" | "DELETE";
  body?: unknown;
  query?: Record<string, string | number | boolean | undefined>;
};

export type ApiClient = <T>(path: string, options?: FetchOptions) => Promise<T>;

export class ApiError extends Error {
  constructor(
    public status: number,
    public problem: ProblemDetails | null,
  ) {
    super(problem?.title ?? `Request failed with status ${status}`);
    this.name = "ApiError";
  }
}

export function createApiClient(
  baseUrl: string,
  getAuthHeader?: () => Promise<string | null>,
): ApiClient {
  return async function apiFetch<T>(path: string, options: FetchOptions = {}): Promise<T> {
    const url = new URL(
      `${baseUrl}${path}`,
      baseUrl.startsWith("/") ? "http://localhost" : undefined,
    );
    for (const [k, v] of Object.entries(options.query ?? {})) {
      if (v !== undefined) url.searchParams.set(k, String(v));
    }

    const headers: Record<string, string> = { "Content-Type": "application/json" };
    const authHeader = await getAuthHeader?.();
    if (authHeader) headers["Authorization"] = authHeader;

    const res = await fetch(baseUrl.startsWith("/") ? `${url.pathname}${url.search}` : url, {
      method: options.method ?? "GET",
      headers,
      body: options.body !== undefined ? JSON.stringify(options.body) : null,
    });

    if (!res.ok) {
      const problem = (await res.json().catch(() => null)) as ProblemDetails | null;
      throw new ApiError(res.status, problem);
    }
    if (res.status === 204) return undefined as T;
    return (await res.json()) as T;
  };
}

/**
 * 服务端组件 / Route Handler 用：直连后端，不经代理（发起方不是浏览器，没有 CORS 问题，见方案 §5.1）。
 *
 * ⚠️ 目前没有任何调用方（所有 `lib/api/*` 都走 createBrowserApiClient + 代理层）。真要用它做
 * 登录用户身份下的 SSR 读取时，**必须传 `getAuthHeader`**：
 *   `createServerApiClient(async () => { const t = await getAccessToken(); return t ? \`Bearer ${t}\` : null; })`
 * 否则请求匿名打到后端、fallback 到 `?userId=` / `Guid.Empty`。不要在本文件里直接 import
 * `lib/auth/session.ts`——它依赖 `next/headers`，而本文件会被 client 组件引用（`users.ts` /
 * `i18n` 等），会让整个 build 失败。把注入逻辑放在调用方（Server Component / Route Handler）里。
 */
export function createServerApiClient(getAuthHeader?: () => Promise<string | null>): ApiClient {
  const baseUrl = process.env["BACKEND_API_BASE_URL"];
  if (!baseUrl) {
    throw new Error("BACKEND_API_BASE_URL is not set — copy .env.local.example to .env.local.");
  }
  return createApiClient(`${baseUrl}/api/v1`, getAuthHeader);
}

/** 客户端组件用：经同源代理 /api/backend，代理层自己注入 token。 */
export function createBrowserApiClient(): ApiClient {
  return createApiClient("/api/backend");
}
