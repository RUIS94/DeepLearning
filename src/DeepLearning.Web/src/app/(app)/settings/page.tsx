"use client";

import { PageShell } from "@/components/shell/page-shell";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { ChangePasswordPanel } from "@/app/(app)/settings/change-password-panel";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Label } from "@/components/ui/label";
import { showToast } from "@/components/ui/toast";
import { useI18n, useT } from "@/lib/i18n";
import { LOCALES, LOCALE_LABELS, type Locale } from "@/lib/i18n/config";

// AI Providers 和全局功能开关(FeatureToggles)已迁到 /admin —— 两者都是纯运营配置(D2'/A1/A4,
// ref/管理员与用户权限隔离_策划书.md),后端现在也把它们收进了 AdminOnly,非admin调用会 403,
// 留在这个所有登录用户都能进的页面里只会让普通用户的 Settings 页报错。
function GeneralSettings() {
  const t = useT();
  const { locale, setLocale } = useI18n();

  function handleChange(next: string) {
    const value = next as Locale;
    if (value === locale) return;
    setLocale(value);
    showToast({ title: t("settings.language.saved"), variant: "success" });
  }

  return (
    <div className="max-w-md space-y-2">
      <Label htmlFor="language-select">{t("settings.language.label")}</Label>
      <Select value={locale} onValueChange={handleChange}>
        <SelectTrigger id="language-select">
          <SelectValue />
        </SelectTrigger>
        <SelectContent>
          {LOCALES.map((code) => (
            <SelectItem key={code} value={code}>
              {LOCALE_LABELS[code]}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
      <p className="text-sm text-muted-foreground">{t("settings.language.description")}</p>
    </div>
  );
}

export default function SettingsPage() {
  const t = useT();
  return (
    <PageShell title={t("settings.title")} back backHref="/practice">
      <Tabs defaultValue="general">
        <TabsList>
          <TabsTrigger value="general">{t("settings.tabGeneral")}</TabsTrigger>
          <TabsTrigger value="account">{t("settings.tabAccount")}</TabsTrigger>
        </TabsList>
        <TabsContent value="general" className="mt-6">
          <GeneralSettings />
        </TabsContent>
        <TabsContent value="account" className="mt-6">
          <ChangePasswordPanel />
        </TabsContent>
      </Tabs>
    </PageShell>
  );
}
