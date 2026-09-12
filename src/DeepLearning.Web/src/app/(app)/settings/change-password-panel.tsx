"use client";

import { useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { Loader2 } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { showToast } from "@/components/ui/toast";
import { useCurrentUser } from "@/hooks/use-current-user";
import { getSupabaseBrowserClient } from "@/lib/auth/supabase-client";
import { changePasswordSchema, type ChangePasswordFormInput } from "@/lib/validation/auth";
import { tFormError, useT } from "@/lib/i18n";

/**
 * 登录态下改密码，和忘记密码流程共用同一套 zod schema（新密码≥6位、两次输入一致），但走的是
 * 完全不同的校验路径：这里要求先验证"现有密码"，Supabase Auth 没有单独一个"只校验密码对不对、
 * 不动 session"的接口，业界通行做法是拿当前用户的 email + 用户刚输入的旧密码去
 * signInWithPassword 一次——密码错就是重新登录失败，密码对就等于验证通过（而且同一个用户重新
 * 登录一次也不会破坏当前 session）。验证通过后才调用 updateUser 落地新密码。
 */
export function ChangePasswordPanel() {
  const t = useT();
  const currentUser = useCurrentUser();
  const [submitError, setSubmitError] = useState<string | null>(null);
  const form = useForm<ChangePasswordFormInput>({
    resolver: zodResolver(changePasswordSchema),
    defaultValues: { currentPassword: "", newPassword: "", confirmPassword: "" },
  });
  const { errors, isSubmitting } = form.formState;

  async function onSubmit(values: ChangePasswordFormInput) {
    setSubmitError(null);
    const supabase = getSupabaseBrowserClient();
    const email = currentUser.data?.email;

    if (!supabase || !email) {
      setSubmitError(t("settings.account.unavailable"));
      return;
    }

    const { error: reauthError } = await supabase.auth.signInWithPassword({
      email,
      password: values.currentPassword,
    });
    if (reauthError) {
      form.setError("currentPassword", { type: "manual", message: "v.currentPasswordIncorrect" });
      return;
    }

    const { error: updateError } = await supabase.auth.updateUser({
      password: values.newPassword,
    });
    if (updateError) {
      setSubmitError(updateError.message);
      return;
    }

    showToast({ title: t("settings.account.changePasswordSuccess"), variant: "success" });
    form.reset();
  }

  return (
    <div className="max-w-md space-y-4">
      <div>
        <h3 className="text-sm font-semibold">{t("settings.account.title")}</h3>
        <p className="text-sm text-muted-foreground">{t("settings.account.description")}</p>
      </div>

      <form className="space-y-4" onSubmit={form.handleSubmit(onSubmit)}>
        <div className="space-y-2">
          <Label htmlFor="currentPassword">{t("settings.account.currentPassword")}</Label>
          <Input id="currentPassword" type="password" {...form.register("currentPassword")} />
          {errors.currentPassword ? (
            <p className="text-xs text-destructive">
              {tFormError(t, errors.currentPassword.message)}
            </p>
          ) : null}
        </div>
        <div className="space-y-2">
          <Label htmlFor="newPassword">{t("settings.account.newPassword")}</Label>
          <Input id="newPassword" type="password" {...form.register("newPassword")} />
          {errors.newPassword ? (
            <p className="text-xs text-destructive">{tFormError(t, errors.newPassword.message)}</p>
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
        <Button type="submit" disabled={isSubmitting}>
          {isSubmitting ? <Loader2 className="size-4 animate-spin" /> : null}
          {isSubmitting ? t("common.saving") : t("settings.account.changePassword")}
        </Button>
      </form>
    </div>
  );
}
