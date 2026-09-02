"use client";

import { PageShell } from "@/components/shell/page-shell";
import { Card, CardContent } from "@/components/ui/card";
import { useCurrentUser } from "@/hooks/use-current-user";

export default function ProfilePage() {
  const currentUser = useCurrentUser();

  return (
    <PageShell title="个人资料" description="账号信息与偏好设置。" back backHref="/practice">
      <Card className="max-w-xl border-border shadow-none">
        <CardContent className="space-y-4 p-6 text-sm">
          <div className="grid grid-cols-[6rem_1fr] gap-x-4 gap-y-3">
            <span className="text-muted-foreground">显示名</span>
            <span>{currentUser.data?.displayName ?? "—"}</span>
            <span className="text-muted-foreground">邮箱</span>
            <span>{currentUser.data?.email ?? "—"}</span>
          </div>
          <p className="border-t border-border pt-4 text-xs text-muted-foreground">
            占位页面，后续接入头像上传、显示名修改等。
          </p>
        </CardContent>
      </Card>
    </PageShell>
  );
}
