import type { Metadata } from "next";
import { LoginPage } from "./login-page";

export const metadata: Metadata = {
  title: { absolute: "译练 · 中英翻译练习与 AI 批改" },
  description:
    "面向 NAATI 认证笔译的翻译练习平台：真题题库、TaskA 翻译与 TaskB 找错标注、AI 分维度批改、追问复核与学习曲线。",
  openGraph: {
    title: "译练 · 中英翻译练习与 AI 批改",
    description: "真题练习、AI 分维度批改与薄弱点追踪，一站式提升中英笔译水平。",
  },
};

export default function Page() {
  return <LoginPage />;
}
