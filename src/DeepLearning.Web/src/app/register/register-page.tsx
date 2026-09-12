"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
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
import { registerSchema, type RegisterFormInput } from "@/lib/validation/auth";
import { tFormError, useT } from "@/lib/i18n";

export function RegisterPage() {
  const t = useT();
  const router = useRouter();
  const supabase = getSupabaseBrowserClient();
  const [submitError, setSubmitError] = useState<string | null>(null);
  // signUp 成功但没有立即拿到 session,说明项目开着"Confirm email"——用户还得去邮箱点确认链接
  // （落地在 app/auth/confirm/route.ts），这里只能先提示"去查收邮件"，不能直接放行进应用。
  const [awaitingConfirmation, setAwaitingConfirmation] = useState(false);
  const form = useForm<RegisterFormInput>({
    resolver: zodResolver(registerSchema),
    defaultValues: { email: "", password: "", confirmPassword: "" },
  });
  const { errors, isSubmitting } = form.formState;

  async function onSubmit(values: RegisterFormInput) {
    setSubmitError(null);

    if (!supabase) {
      // 与登录页一致的原型态占位行为：Supabase 没配置时不做真实注册,直接放进应用。
      setTimeout(() => router.push("/practice"), 500);
      return;
    }

    const { data, error } = await supabase.auth.signUp({
      email: values.email,
      password: values.password,
      options: { emailRedirectTo: `${window.location.origin}/auth/confirm` },
    });
    if (error) {
      setSubmitError(error.message);
      return;
    }
    if (data.session) {
      // 项目关掉了"Confirm email"——signUp 直接给了 session，和登录成功走一样的路径。
      router.push("/practice");
      router.refresh();
      return;
    }
    setAwaitingConfirmation(true);
  }

  if (awaitingConfirmation) {
    return (
      <AuthShell>
        <h1 className="text-2xl font-semibold">{t("register.title")}</h1>
        <Alert className="mt-6 border-none">
          <AlertDescription>{t("register.checkEmail")}</AlertDescription>
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
      <h1 className="text-2xl font-semibold">{t("register.title")}</h1>
      <p className="mt-1 text-sm text-muted-foreground">
        {supabase ? t("register.subtitleSupabase") : t("login.subtitleMock")}
      </p>
      <form className="mt-6 space-y-4" onSubmit={form.handleSubmit(onSubmit)}>
        <div className="space-y-2">
          <Label htmlFor="email">{t("login.email")}</Label>
          <Input id="email" type="email" {...form.register("email")} />
          {errors.email ? (
            <p className="text-xs text-destructive">{tFormError(t, errors.email.message)}</p>
          ) : null}
        </div>
        <div className="space-y-2">
          <Label htmlFor="password">{t("login.password")}</Label>
          <Input id="password" type="password" {...form.register("password")} />
          {errors.password ? (
            <p className="text-xs text-destructive">{tFormError(t, errors.password.message)}</p>
          ) : null}
        </div>
        <div className="space-y-2">
          <Label htmlFor="confirmPassword">{t("register.confirmPassword")}</Label>
          <Input id="confirmPassword" type="password" {...form.register("confirmPassword")} />
          {errors.confirmPassword ? (
            <p className="text-xs text-destructive">
              {tFormError(t, errors.confirmPassword.message)}
            </p>
          ) : null}
        </div>
        {submitError ? (
          <Alert variant="destructive">
            <AlertDescription>{submitError}</AlertDescription>
          </Alert>
        ) : null}
        <Button type="submit" className="w-full" disabled={isSubmitting}>
          {isSubmitting ? <Loader2 className="size-4 animate-spin" /> : null}
          {isSubmitting ? t("register.submitting") : t("register.submit")}
        </Button>
      </form>
      <p className="mt-4 text-center text-xs text-muted-foreground">
        {t("register.loginPrefix")}
        <Link href="/" className="ml-1 text-primary underline underline-offset-2">
          {t("register.loginLink")}
        </Link>
      </p>
    </AuthShell>
  );
}
