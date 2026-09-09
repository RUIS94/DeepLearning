import { readFileSync } from "node:fs";
import { defineConfig, devices } from "@playwright/test";

/**
 * E2E 冒烟：一条真实链路 `登录 → 题库 → 打开题目 → 提交作答 → 看到批改进行中`，
 * 顺带实测 C1(提交后 submission.userId 是真实 Supabase 用户 id,而不是 FALLBACK_USER_ID)。
 *
 * 前置(Playwright 不代管的部分):
 *   1. 后端指向 Supabase 跑着:
 *        dotnet run --project src/DeepLearning.Api --launch-profile "http (Supabase)"
 *   2. .env.local 里 NEXT_PUBLIC_SUPABASE_URL / NEXT_PUBLIC_SUPABASE_ANON_KEY / BACKEND_API_BASE_URL 已填。
 *   3. .env.e2e.local 里 E2E_USER_EMAIL / E2E_USER_PASSWORD 已填(gitignored)。
 * 前端由下面的 webServer 自动 `npm run dev`(已在跑则复用)。
 *
 * 评卷是分钟级(4 次 LLM 调用),所以冒烟只断言到"提交成功 + userId 正确 + 进入批改中";
 * "看到 Band 结果"是可选长测(spec 里标了 test.slow / 宽超时)。
 */

// 极简 .env 读取——不引 dotenv 依赖。只认 KEY=VALUE,忽略注释/空行。
for (const line of safeRead(".env.e2e.local").split("\n")) {
  const m = line.match(/^\s*([A-Z0-9_]+)\s*=\s*(.*?)\s*$/);
  if (!m) continue;
  const [, key, value] = m;
  if (key && !(key in process.env)) process.env[key] = value ?? "";
}

function safeRead(path: string): string {
  try {
    return readFileSync(new URL(path, import.meta.url), "utf8");
  } catch {
    return "";
  }
}

export default defineConfig({
  testDir: "./e2e",
  fullyParallel: false,
  workers: 1,
  retries: 0,
  timeout: 90_000,
  expect: { timeout: 15_000 },
  reporter: [["list"]],
  use: {
    baseURL: "http://localhost:3000",
    trace: "retain-on-failure",
    screenshot: "only-on-failure",
  },
  projects: [{ name: "chromium", use: { ...devices["Desktop Chrome"] } }],
  webServer: {
    command: "npm run dev",
    url: "http://localhost:3000",
    reuseExistingServer: true,
    timeout: 120_000,
  },
});
