/**
 * 前端 UI 语言开关的公共常量。默认英文；用户在 设置 → General 里切换，登录用户的选择
 * 会通过 PUT /users/{id}/language-preference 落库（users.language_preference），未登录时
 * 只存在 localStorage。这只切界面文案，不影响任何存储内容（题目、评分、AI 输出可能仍是中文）。
 */
export const LOCALES = ["en", "zh"] as const;

export type Locale = (typeof LOCALES)[number];

export const DEFAULT_LOCALE: Locale = "en";

/** 未登录 / 尚未拿到 profile 时，语言选择暂存在这个 localStorage key 下。 */
export const LOCALE_STORAGE_KEY = "yilian.locale";

export function isLocale(value: unknown): value is Locale {
  return typeof value === "string" && (LOCALES as readonly string[]).includes(value);
}

export function normalizeLocale(value: unknown): Locale {
  return isLocale(value) ? value : DEFAULT_LOCALE;
}

export const LOCALE_LABELS: Record<Locale, string> = {
  en: "English",
  zh: "中文",
};
