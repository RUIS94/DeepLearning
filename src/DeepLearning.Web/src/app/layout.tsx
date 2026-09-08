import type { Metadata } from "next";
import type { ReactNode } from "react";
import { Providers } from "./providers";
import "./globals.css";

// 根布局保持静态（英文），不读 cookie —— 否则整站都会退化成按需渲染。运行时界面语言由
// 客户端的 I18nProvider 接管：它 hydration 后会把 <html lang> 改成当前语言，标签页标题也随
// 各页面自己的 generateMetadata（读 cookie）切换。
export const metadata: Metadata = {
  title: { default: "Deep Learning", template: "%s · Deep Learning" },
  description:
    "NAATI translation practice platform with real-exam questions, AI grading, error review, and learning progress tracking.",
  icons: { icon: "/favicon.ico" },
};

export default function RootLayout({ children }: { children: ReactNode }) {
  return (
    <html lang="en">
      <body>
        <Providers>{children}</Providers>
      </body>
    </html>
  );
}
