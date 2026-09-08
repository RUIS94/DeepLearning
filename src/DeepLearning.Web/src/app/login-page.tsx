"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import { GraduationCap, Loader2 } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Card, CardContent } from "@/components/ui/card";
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
    return (
      <div className="flex min-h-screen items-center justify-center bg-background">
        <Loader2 className="size-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  return (
    <div className="grid min-h-screen lg:grid-cols-2">
      <div className="hidden flex-col justify-between bg-primary p-12 text-primary-foreground lg:flex">
        <div className="flex items-center gap-2 font-serif text-lg font-semibold">
          <GraduationCap className="size-6" />
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
        <p className="text-xs opacity-60">
          {supabase ? t("login.footerRealBackend") : t("login.footerMock")}
        </p>
      </div>

      <div className="flex items-center justify-center p-6">
        <Card className="w-full max-w-sm border-border shadow-none">
          <CardContent className="p-8">
            <h1 className="text-2xl font-semibold">{t("login.title")}</h1>
            <p className="mt-1 text-sm text-muted-foreground">
              {supabase ? t("login.subtitleSupabase") : t("login.subtitleMock")}
            </p>
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
                <Label htmlFor="password">{t("login.password")}</Label>
                <Input
                  id="password"
                  type="password"
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  required
                />
              </div>
              {error ? <p className="text-xs text-destructive">{error}</p> : null}
              <Button type="submit" className="w-full" disabled={pending}>
                {pending ? <Loader2 className="size-4 animate-spin" /> : null}
                {pending ? t("login.submitting") : t("login.submit")}
              </Button>
            </form>
            {!supabase ? (
              <p className="mt-4 text-center text-xs text-muted-foreground">
                {t("login.browsePrefix")}
                <Link href="/practice" className="ml-1 text-primary underline underline-offset-2">
                  {t("login.browseLink")}
                </Link>
              </p>
            ) : null}
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
