"use client";

import Script from "next/script";
import { useCallback, useId, useRef } from "react";

/**
 * 注册页专用（app/register/register-page.tsx）——NEXT_PUBLIC_TURNSTILE_SITE_KEY 没配置时整个
 * 组件渲染成 null，跟本仓库其它"Supabase/Upstash 未配置时优雅降级"的约定一致（本地开发默认没有
 * Cloudflare Turnstile 账号可用）。真正拦截在服务端——app/api/auth-guard/[action]/route.ts 校验
 * token，客户端这层只是拿到 token 交给它。
 */

declare global {
  interface Window {
    turnstile?: {
      render: (
        container: string | HTMLElement,
        options: {
          sitekey: string;
          callback: (token: string) => void;
          "expired-callback"?: () => void;
          "error-callback"?: () => void;
        },
      ) => string;
    };
  }
}

export function TurnstileWidget({ onToken }: { onToken: (token: string | null) => void }) {
  // useId() 产出的 ":r0:" 这类值不是合法的 CSS id/选择器，冒号要去掉。
  const containerId = `turnstile-${useId().replace(/:/g, "")}`;
  const widgetRendered = useRef(false);

  const renderWidget = useCallback(() => {
    const siteKey = process.env["NEXT_PUBLIC_TURNSTILE_SITE_KEY"];
    if (!siteKey || !window.turnstile || widgetRendered.current) return;
    widgetRendered.current = true;
    window.turnstile.render(`#${containerId}`, {
      sitekey: siteKey,
      callback: (token) => onToken(token),
      "expired-callback": () => onToken(null),
      "error-callback": () => onToken(null),
    });
  }, [containerId, onToken]);

  if (!process.env["NEXT_PUBLIC_TURNSTILE_SITE_KEY"]) return null;

  return (
    <>
      <Script
        src="https://challenges.cloudflare.com/turnstile/v0/api.js"
        strategy="afterInteractive"
        onReady={renderWidget}
      />
      <div id={containerId} />
    </>
  );
}
