import type { Metadata } from "next";
import type { ReactNode } from "react";
import { Providers } from "./providers";
import "./globals.css";

export const metadata: Metadata = {
  title: { default: "译练", template: "%s · 译练" },
  description:
    "面向 NAATI 认证笔译的翻译练习平台：真题题库、TaskA 翻译与 TaskB 找错标注、AI 分维度批改、追问复核与学习曲线。",
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
