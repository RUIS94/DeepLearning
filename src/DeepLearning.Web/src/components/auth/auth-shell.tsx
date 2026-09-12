"use client";

import type { ReactNode } from "react";
import { Loader2 } from "lucide-react";
import { Card, CardContent } from "@/components/ui/card";
import { useT } from "@/lib/i18n";

/** 登录页"正在核对已有 session"、重置密码页"正在核对邮件链接里的恢复 session"共用的全屏
 * 居中 spinner——两边都是"页面刚加载,得先等一次异步 session 检查,之前不能露出表单"。 */
export function AuthLoading() {
  return (
    <div className="flex min-h-screen items-center justify-center bg-background">
      <Loader2 className="size-6 animate-spin text-muted-foreground" />
    </div>
  );
}

/**
 * 登录 / 注册 / 忘记密码 / 重置密码四个页面共用的分屏外壳:左侧品牌 hero(四个页面文案完全
 * 一致,原来只写在 login-page.tsx 里),右侧卡片放各自的表单(通过 children 传入)。
 * `footer` 是左下角那行小字,各页面可选传自己的版本(登录页用来区分真实后端/mock 原型),
 * 不传就留空。
 */
export function AuthShell({ footer, children }: { footer?: ReactNode; children: ReactNode }) {
  const t = useT();
  return (
    <div className="grid min-h-screen lg:grid-cols-2">
      <div className="hidden flex-col justify-between bg-primary p-12 text-primary-foreground lg:flex">
        <div className="flex items-center gap-2 font-serif text-lg font-semibold">
          <img src="/logo-dark.svg" alt="" className="size-6" />
          {t("login.brand")}
        </div>
        <div className="space-y-6">
          <h2 className="font-serif text-4xl leading-snug text-primary-foreground">
            {t("login.heroTitle")}
          </h2>
          <p className="max-w-md text-sm leading-relaxed opacity-80">{t("login.heroBody")}</p>
          <dl className="grid grid-cols-3 gap-6 border-t border-primary-foreground/20 pt-6 text-sm">
            <div>
              <dt className="opacity-70">{t("login.statDimensions")}</dt>
              <dd className="text-numeric mt-1 text-2xl font-semibold">3</dd>
            </div>
            <div>
              <dt className="opacity-70">{t("login.statTaskTypes")}</dt>
              <dd className="text-numeric mt-1 text-2xl font-semibold">2</dd>
            </div>
            <div>
              <dt className="opacity-70">{t("login.statBandScale")}</dt>
              <dd className="text-numeric mt-1 text-2xl font-semibold">1–5</dd>
            </div>
          </dl>
        </div>
        <p className="text-xs opacity-60">{footer}</p>
      </div>

      <div className="flex items-center justify-center p-6">
        <Card className="w-full max-w-sm border-border shadow-none">
          <CardContent className="p-8">{children}</CardContent>
        </Card>
      </div>
    </div>
  );
}
