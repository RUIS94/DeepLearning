import { test, expect } from "@playwright/test";

/**
 * 端到端冒烟 + C1 实测。
 *
 * 链路:真实 Supabase 登录 → 题库(筛到 TaskA) → 打开一题 → 填中文译文 → 提交 → 落到
 * /submissions/[id]。断言:提交成功 + 批改已启动(状态离开 draft) + **submission.userId 是
 * 真实 Supabase 用户 id,而不是 FALLBACK_USER_ID** —— 这一条就是 Section C 的 C1 隐患实测。
 *
 * 不等 Band 结果(评卷是分钟级的四次 LLM 调用)。
 *
 * 前置见 playwright.config.ts 顶部注释(后端要指向 Supabase 跑着;账号在 .env.e2e.local)。
 */

const FALLBACK_USER_ID = "11111111-1111-4111-8111-111111111111";
const EMAIL = process.env["E2E_USER_EMAIL"];
const PASSWORD = process.env["E2E_USER_PASSWORD"];

test.skip(
  //!EMAIL || !PASSWORD,
  //"E2E_USER_EMAIL / E2E_USER_PASSWORD not set — put them in src/DeepLearning.Web/.env.e2e.local",
  true,
  "Intentionally disabled — this spec writes real submissions to Supabase. Re-enable only after confirming target DB and adding post-test cleanup.",
);

test("login → open a TaskA question → submit → grading starts as the real Supabase user (C1)", async ({
  page,
}) => {
  test.slow(); // login + a real submission round-trip; generous but bounded.

  // 1. Login with the real Supabase account.
  await page.goto("/");
  await page.locator("#email").fill(EMAIL!);
  await page.locator("#password").fill(PASSWORD!);
  await page.getByRole("button", { name: /sign in/i }).click();
  await page.waitForURL("**/practice", { timeout: 30_000 });

  // 2. Narrow the list to TaskA so the answer page has a translation textarea.
  await page.getByRole("combobox").first().click();
  await page.getByRole("option", { name: "TaskA", exact: true }).click();

  const startLink = page.locator('a[href^="/practice/"]').first();
  await expect(startLink).toBeVisible({ timeout: 20_000 });
  await startLink.click();
  await page.waitForURL(/\/practice\/[0-9a-f-]{36}$/i);

  // 3. Fill a TaskA translation.
  const translation = page.getByPlaceholder(/translation/i);
  await expect(translation).toBeVisible({ timeout: 20_000 });
  await translation.fill(
    "这是一段用于端到端冒烟测试的中文译文,只为验证提交链路能以真实用户身份把 submission 落库。",
  );

  // 4. Submit — capture the POST response so we have the new submission id.
  const createResponse = page.waitForResponse(
    (r) => r.url().includes("/api/backend/submissions") && r.request().method() === "POST",
  );
  await page.getByRole("button", { name: /submit and start grading/i }).click();
  const created = await (await createResponse).json();
  expect(created.id, "POST /submissions returned an id").toBeTruthy();

  await page.waitForURL(/\/submissions\/[0-9a-f-]{36}$/i, { timeout: 20_000 });

  // 5. C1: the submission must be owned by the real logged-in user, not the prototype stub.
  const detailResponse = await page.waitForResponse(
    (r) =>
      r.url().includes(`/api/backend/submissions/${created.id}`) &&
      r.request().method() === "GET" &&
      r.ok(),
    { timeout: 20_000 },
  );
  const detail = await detailResponse.json();

  expect(detail.userId, "submission is NOT owned by FALLBACK_USER_ID").not.toBe(FALLBACK_USER_ID);
  expect(detail.userId).toMatch(/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i);
  expect(detail.status, "grading has started (status left draft)").not.toBe(0);
  expect(detail.submittedAt).toBeTruthy();

  // The submission page shows the grading-in-progress state (we don't wait for the Band).
  await expect(page.getByText(/grading/i).first()).toBeVisible({ timeout: 20_000 });
});
