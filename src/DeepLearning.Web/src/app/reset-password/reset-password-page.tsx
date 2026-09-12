"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { Loader2 } from "lucide-react";
import { AuthLoading, AuthShell } from "@/components/auth/auth-shell";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { getSupabaseBrowserClient } from "@/lib/auth/supabase-client";
import { resetPasswordSchema, type ResetPasswordFormInput } from "@/lib/validation/auth";
import { tFormError, useT } from "@/lib/i18n";

export function ResetPasswordPage() {
  const t = useT();
  const router = useRouter();
  const supabase = getSupabaseBrowserClient();
  const [submitError, setSubmitError] = useState<string | null>(null);
  // app/auth/confirm/route.ts 已经在服务端把邮件里的恢复链接换成了一个真实 session（写进
  // cookie）——落地这个页面时,浏览器端 client 初始化时会自己从 cookie 里把它读出来。
  // 没有 supabase（原型态）视为直接可用；真正接了 Supabase 但等了一圈还没等到 session,
  // 说明链接已过期/用过——不能让 spinner 转到天荒地老。
  const [status, setStatus] = useState<"checking" | "ready" | "invalid">(
    supabase ? "checking" : "ready",
  );
  const form = useForm<ResetPasswordFormInput>({
    resolver: zodResolver(resetPasswordSchema),
    defaultValues: { password: "", confirmPassword: "" },
  });
  const { errors, isSubmitting } = form.formState;

  useEffect(() => {
    if (!supabase) return;
    let active = true;
    supabase.auth.getSession().then(({ data }) => {
      if (active && data.session) setStatus("ready");
    });
    // getSession() 有时会在 PASSWORD_RECOVERY 事件真正落地前跑完——用 onAuthStateChange 兜底。
    const {
      data: { subscription },
    } = supabase.auth.onAuthStateChange((event) => {
      if (event === "PASSWORD_RECOVERY") setStatus("ready");
    });
    // 链接过期/已被用过时不会有任何 session 出现——给够时间后就别再空转,改成明确提示。
    const timeout = setTimeout(() => {
      if (active) setStatus((current) => (current === "checking" ? "invalid" : current));
    }, 3000);
    return () => {
      active = false;
      subscription.unsubscribe();
      clearTimeout(timeout);
    };
  }, [supabase]);

  async function onSubmit(values: ResetPasswordFormInput) {
    setSubmitError(null);

    if (!supabase) {
      router.push("/practice");
      return;
    }

    const { error } = await supabase.auth.updateUser({ password: values.password });
    if (error) {
      setSubmitError(error.message);
      return;
    }
    router.push("/practice");
    router.refresh();
  }

  if (status === "checking") {
    return <AuthLoading />;
  }

  if (status === "invalid") {
    return (
      <AuthShell>
        <h1 className="text-2xl font-semibold">{t("resetPassword.title")}</h1>
        <Alert variant="destructive" className="mt-6">
          <AlertDescription>{t("resetPassword.linkInvalid")}</AlertDescription>
        </Alert>
        <p className="mt-4 text-center text-xs text-muted-foreground">
          <Link href="/forgot-password" className="text-primary underline underline-offset-2">
            {t("resetPassword.requestNewLink")}
          </Link>
        </p>
      </AuthShell>
    );
  }

  return (
    <AuthShell footer={supabase ? t("login.footerRealBackend") : t("login.footerMock")}>
      <h1 className="text-2xl font-semibold">{t("resetPassword.title")}</h1>
      <p className="mt-1 text-sm text-muted-foreground">{t("resetPassword.subtitle")}</p>
      <form className="mt-6 space-y-4" onSubmit={form.handleSubmit(onSubmit)}>
        <div className="space-y-2">
          <Label htmlFor="password">{t("resetPassword.newPassword")}</Label>
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
          {isSubmitting ? t("resetPassword.submitting") : t("resetPassword.submit")}
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
