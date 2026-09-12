"use client";

import Link from "next/link";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { Loader2 } from "lucide-react";
import { AuthShell } from "@/components/auth/auth-shell";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { getSupabaseBrowserClient } from "@/lib/auth/supabase-client";
import { forgotPasswordSchema, type ForgotPasswordFormInput } from "@/lib/validation/auth";
import { tFormError, useT } from "@/lib/i18n";

export function ForgotPasswordPage() {
  const t = useT();
  const supabase = getSupabaseBrowserClient();
  const [submitError, setSubmitError] = useState<string | null>(null);
  const [sent, setSent] = useState(false);
  const form = useForm<ForgotPasswordFormInput>({
    resolver: zodResolver(forgotPasswordSchema),
    defaultValues: { email: "" },
  });
  const { errors, isSubmitting } = form.formState;

  async function onSubmit(values: ForgotPasswordFormInput) {
    setSubmitError(null);

    if (!supabase) {
      // Supabase 没配置——原型阶段没有真实账户体系可重置，直接给出和真实流程一致的成功提示。
      setSent(true);
      return;
    }

    // next 落地页由 app/auth/confirm/route.ts 按 type=recovery 自己决定跳 /reset-password，
    // 这里的 redirectTo 只是把 Supabase 邮件模板里 {{ .RedirectTo }} 变量指过来，不依赖它也能工作。
    // Supabase 自己就不会告诉调用方"这个邮箱是否真的注册过"（邮箱不存在时同样返回成功），
    // 这里不需要再额外做一层"不管结果都提示成功"的处理。
    const { error } = await supabase.auth.resetPasswordForEmail(values.email, {
      redirectTo: `${window.location.origin}/auth/confirm`,
    });
    if (error) {
      setSubmitError(error.message);
      return;
    }
    setSent(true);
  }

  if (sent) {
    return (
      <AuthShell>
        <h1 className="text-2xl font-semibold">{t("forgotPassword.title")}</h1>
        <Alert className="mt-6">
          <AlertDescription>{t("forgotPassword.checkEmail")}</AlertDescription>
        </Alert>
        <p className="mt-4 text-center text-xs text-muted-foreground">
          <Link href="/" className="text-primary underline underline-offset-2">
            {t("register.backToLogin")}
          </Link>
        </p>
      </AuthShell>
    );
  }

  return (
    <AuthShell footer={supabase ? t("login.footerRealBackend") : t("login.footerMock")}>
      <h1 className="text-2xl font-semibold">{t("forgotPassword.title")}</h1>
      <p className="mt-1 text-sm text-muted-foreground">{t("forgotPassword.subtitle")}</p>
      <form className="mt-6 space-y-4" onSubmit={form.handleSubmit(onSubmit)}>
        <div className="space-y-2">
          <Label htmlFor="email">{t("login.email")}</Label>
          <Input id="email" type="email" {...form.register("email")} />
          {errors.email ? (
            <p className="text-xs text-destructive">{tFormError(t, errors.email.message)}</p>
          ) : null}
        </div>
        {submitError ? (
          <Alert variant="destructive">
            <AlertDescription>{submitError}</AlertDescription>
          </Alert>
        ) : null}
        <Button type="submit" className="w-full" disabled={isSubmitting}>
          {isSubmitting ? <Loader2 className="size-4 animate-spin" /> : null}
          {isSubmitting ? t("forgotPassword.submitting") : t("forgotPassword.submit")}
        </Button>
      </form>
      <p className="mt-4 text-center text-xs text-muted-foreground">
        <Link href="/" className="text-primary underline underline-offset-2">
          {t("register.backToLogin")}
        </Link>
      </p>
    </AuthShell>
  );
}
