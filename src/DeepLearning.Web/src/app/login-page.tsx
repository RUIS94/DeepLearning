"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import { Loader2 } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { AuthLoading, AuthShell } from "@/components/auth/auth-shell";
import { getSupabaseBrowserClient } from "@/lib/auth/supabase-client";
import { useT } from "@/lib/i18n";

export function LoginPage() {
  const t = useT();
  const router = useRouter();
  const supabase = getSupabaseBrowserClient();
  const [email, setEmail] = useState(supabase ? "" : "learner@example.com");
  const [password, setPassword] = useState("");
  const [pending, setPending] = useState(false);
  const [error, setError] = useState<string | null>(null);
  // 已登录还落在 "/"（比如手动输入 localhost:3000）时，直接进应用。
  // 用浏览器端 client 判断——比只靠 proxy 层的服务端 getUser() 更可靠（后者可能因
  // 服务端拿不到 cookie / 校验请求失败而误判为未登录）。
  const [checkingSession, setCheckingSession] = useState(Boolean(supabase));
  // /auth/confirm 校验邮件链接失败(过期/已用过)时会带这个参数跳回登录页——用 URLSearchParams
  // 而不是 useSearchParams()，这样这页不需要额外套 Suspense 边界。
  const [linkInvalid, setLinkInvalid] = useState(false);

  useEffect(() => {
    if (new URLSearchParams(window.location.search).get("authError") === "link_invalid") {
      setLinkInvalid(true);
    }
  }, []);

  useEffect(() => {
    if (!supabase) return;
    let active = true;
    supabase.auth.getSession().then(({ data }) => {
      if (!active) return;
      if (data.session) {
        router.replace("/practice");
      } else {
        setCheckingSession(false);
      }
    });
    return () => {
      active = false;
    };
  }, [supabase, router]);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    setPending(true);

    if (!supabase) {
      // Supabase 还没配置（NEXT_PUBLIC_SUPABASE_ANON_KEY 缺失）——保留原型阶段的占位行为，
      // 之后各页面会用 FALLBACK_USER_ID（见 hooks/use-current-user.ts）当作当前用户。
      setTimeout(() => router.push("/practice"), 500);
      return;
    }

    const { error: signInError } = await supabase.auth.signInWithPassword({ email, password });
    setPending(false);
    if (signInError) {
      setError(signInError.message);
      return;
    }
    router.push("/practice");
    router.refresh();
  }

  if (checkingSession) {
    return <AuthLoading />;
  }

  return (
    <AuthShell footer={supabase ? t("login.footerRealBackend") : t("login.footerMock")}>
      <h1 className="text-2xl font-semibold">{t("login.title")}</h1>
      <p className="mt-1 text-sm text-muted-foreground">
        {supabase ? t("login.subtitleSupabase") : t("login.subtitleMock")}
      </p>
      {linkInvalid ? (
        <Alert variant="destructive" className="mt-4">
          <AlertDescription>{t("login.linkInvalid")}</AlertDescription>
        </Alert>
      ) : null}
      <form className="mt-6 space-y-4" onSubmit={handleSubmit}>
        <div className="space-y-2">
          <Label htmlFor="email">{t("login.email")}</Label>
          <Input
            id="email"
            type="email"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            required
          />
        </div>
        <div className="space-y-2">
          <div className="flex items-center justify-between">
            <Label htmlFor="password">{t("login.password")}</Label>
            <Link
              href="/forgot-password"
              className="text-xs text-primary underline underline-offset-2"
            >
              {t("login.forgotPassword")}
            </Link>
          </div>
          <Input
            id="password"
            type="password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            required
          />
        </div>
        {error ? (
          <Alert variant="destructive">
            <AlertDescription>{error}</AlertDescription>
          </Alert>
        ) : null}
        <Button type="submit" className="w-full" disabled={pending}>
          {pending ? <Loader2 className="size-4 animate-spin" /> : null}
          {pending ? t("login.submitting") : t("login.submit")}
        </Button>
      </form>
      <p className="mt-4 text-center text-xs text-muted-foreground">
        {t("login.registerPrefix")}
        <Link href="/register" className="ml-1 text-primary underline underline-offset-2">
          {t("login.registerLink")}
        </Link>
      </p>
      {!supabase ? (
        <p className="mt-2 text-center text-xs text-muted-foreground">
          {t("login.browsePrefix")}
          <Link href="/practice" className="ml-1 text-primary underline underline-offset-2">
            {t("login.browseLink")}
          </Link>
        </p>
      ) : null}
    </AuthShell>
  );
}
