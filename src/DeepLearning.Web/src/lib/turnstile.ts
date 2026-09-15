/**
 * 服务端校验 Cloudflare Turnstile token（siteverify）。TURNSTILE_SECRET_KEY 缺失时优雅降级为
 * "直接放行"——跟 lib/rate-limit/limiter.ts 里 Upstash 未配置时的降级方式一致，本地开发默认没有
 * Cloudflare Turnstile 账号可用。真正校验只在配置齐全（生产环境）时才生效。
 */
const VERIFY_URL = "https://challenges.cloudflare.com/turnstile/v0/siteverify";

export async function verifyTurnstileToken(
  token: string | undefined,
  remoteIp: string,
): Promise<boolean> {
  const secret = process.env["TURNSTILE_SECRET_KEY"];
  if (!secret) return true;
  if (!token) return false;

  const res = await fetch(VERIFY_URL, {
    method: "POST",
    headers: { "Content-Type": "application/x-www-form-urlencoded" },
    body: new URLSearchParams({ secret, response: token, remoteip: remoteIp }),
  });
  if (!res.ok) return false;

  const data = (await res.json()) as { success?: boolean };
  return data.success === true;
}
