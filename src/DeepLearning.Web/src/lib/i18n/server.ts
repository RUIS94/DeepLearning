import { cookies } from "next/headers";
import { DEFAULT_LOCALE, LOCALE_STORAGE_KEY, normalizeLocale, type Locale } from "./config";
import en, { type MessageKey } from "./messages/en";
import zh from "./messages/zh";

const MESSAGES: Record<Locale, Record<MessageKey, string>> = { en, zh };

/**
 * 服务端（generateMetadata 等）用的界面语言。只读 `I18nProvider` 镜像出来的 cookie；
 * 拿不到就退回默认（en）。运行时界面语言仍以客户端的 localStorage / 用户 profile 为准，
 * 这里只影响 SSR 阶段的 <title> / OG。
 */
export async function getServerLocale(): Promise<Locale> {
  try {
    const store = await cookies();
    return normalizeLocale(store.get(LOCALE_STORAGE_KEY)?.value);
  } catch {
    return DEFAULT_LOCALE;
  }
}

/** 服务端翻译函数（无插值，metadata 文案都不带参数）。 */
export function serverT(locale: Locale): (key: MessageKey) => string {
  const table = MESSAGES[locale] ?? MESSAGES[DEFAULT_LOCALE];
  return (key) => table[key] ?? MESSAGES[DEFAULT_LOCALE][key] ?? key;
}
