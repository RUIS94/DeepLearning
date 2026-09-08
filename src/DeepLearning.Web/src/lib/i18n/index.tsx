"use client";

import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useRef,
  useState,
  type ReactNode,
} from "react";
import { useQueryClient } from "@tanstack/react-query";
import { useCurrentUser } from "@/hooks/use-current-user";
import { updateUserLanguagePreference } from "@/lib/api/users";
import { DEFAULT_LOCALE, LOCALE_STORAGE_KEY, normalizeLocale, type Locale } from "./config";
import en, { type MessageKey } from "./messages/en";
import zh from "./messages/zh";

const MESSAGES: Record<Locale, Record<MessageKey, string>> = { en, zh };

export type TranslateFn = (key: MessageKey, vars?: Record<string, string | number>) => string;

interface I18nContextValue {
  locale: Locale;
  setLocale: (next: Locale) => void;
  t: TranslateFn;
}

const I18nContext = createContext<I18nContextValue | null>(null);

function readStoredLocale(): Locale {
  if (typeof window === "undefined") return DEFAULT_LOCALE;
  try {
    return normalizeLocale(window.localStorage.getItem(LOCALE_STORAGE_KEY));
  } catch {
    return DEFAULT_LOCALE;
  }
}

function interpolate(template: string, vars?: Record<string, string | number>): string {
  if (!vars) return template;
  return template.replace(/\{(\w+)\}/g, (match, name: string) =>
    name in vars ? String(vars[name]) : match,
  );
}

/**
 * 界面语言的单一来源。优先级：已登录用户的 users.language_preference > localStorage > 默认（en）。
 * 切换时立刻更新本地并写 localStorage；已登录则顺带 PUT 落库（失败不回滚本地，只是下次刷新会
 * 用服务端的旧值）。必须嵌在 QueryClientProvider 内部（用到 useCurrentUser）。
 */
export function I18nProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient();
  const currentUser = useCurrentUser();
  const serverLocale = currentUser.data?.languagePreference ?? null;

  const [locale, setLocaleState] = useState<Locale>(DEFAULT_LOCALE);

  // 首帧后用 localStorage 里的值补上（避免 SSR / 首帧闪烁不一致）。
  useEffect(() => {
    setLocaleState(readStoredLocale());
  }, []);

  // 服务端偏好一旦到手且与本地不同，以服务端为准（跨设备一致）。
  const lastServerLocale = useRef<string | null>(null);
  useEffect(() => {
    if (!serverLocale || serverLocale === lastServerLocale.current) return;
    lastServerLocale.current = serverLocale;
    const next = normalizeLocale(serverLocale);
    setLocaleState(next);
    try {
      window.localStorage.setItem(LOCALE_STORAGE_KEY, next);
    } catch {
      /* localStorage 不可用时忽略 */
    }
  }, [serverLocale]);

  useEffect(() => {
    if (typeof document !== "undefined") {
      document.documentElement.lang = locale;
      // 同一份值镜像到 cookie，仅供 SSR 的 generateMetadata 读取浏览器标签页标题的语言
      // （运行时界面语言仍以 localStorage / 用户 profile 为准）。
      try {
        document.cookie = `${LOCALE_STORAGE_KEY}=${locale}; path=/; max-age=31536000; samesite=lax`;
      } catch {
        /* 忽略 */
      }
    }
  }, [locale]);

  const setLocale = useCallback(
    (next: Locale) => {
      setLocaleState(next);
      try {
        window.localStorage.setItem(LOCALE_STORAGE_KEY, next);
      } catch {
        /* 忽略 */
      }
      const userId = currentUser.data?.id;
      // FALLBACK 用户 id 在真实库里没有对应行，PUT 会 404 —— 只在真正登录时落库。
      if (userId && serverLocale !== null) {
        lastServerLocale.current = next;
        updateUserLanguagePreference(userId, next)
          .then(() => {
            queryClient.invalidateQueries({ queryKey: ["current-user"] });
          })
          .catch(() => {
            /* 落库失败：本地已生效，不打扰用户 */
          });
      }
    },
    [currentUser.data?.id, serverLocale, queryClient],
  );

  const t = useCallback<TranslateFn>(
    (key, vars) => {
      const table = MESSAGES[locale] ?? MESSAGES[DEFAULT_LOCALE];
      const template = table[key] ?? MESSAGES[DEFAULT_LOCALE][key] ?? key;
      return interpolate(template, vars);
    },
    [locale],
  );

  const value = useMemo<I18nContextValue>(() => ({ locale, setLocale, t }), [locale, setLocale, t]);

  return <I18nContext.Provider value={value}>{children}</I18nContext.Provider>;
}

/**
 * 把 zod schema 里的校验消息翻译出来。约定：以 `v.` 开头的是 i18n key（见 messages），
 * 带参数的写成 `v.key::<len>`（目前只有 rangeWithinLength 用到 {len}）。其它字符串（zod 内建
 * 提示等）原样返回。
 */
export function tFormError(t: TranslateFn, message: string | undefined): string | undefined {
  if (!message || !message.startsWith("v.")) return message;
  const [key, len] = message.split("::");
  return t(key as MessageKey, len !== undefined ? { len } : undefined);
}

export function useI18n(): I18nContextValue {
  const ctx = useContext(I18nContext);
  if (!ctx) {
    throw new Error("useI18n must be used within <I18nProvider>");
  }
  return ctx;
}

/** 只要翻译函数时的简写：`const t = useT();` */
export function useT(): TranslateFn {
  return useI18n().t;
}

export type { Locale, MessageKey };
