"use client";

import { useState } from "react";
import Link from "next/link";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { PageShell } from "@/components/shell/page-shell";
import { GlobalFeatureToggles } from "@/components/admin/global-feature-toggles";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { ConfirmDialog } from "@/components/ui/confirm-dialog";
import { ErrorBanner } from "@/components/shared/ai-loading-state";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { showToast } from "@/components/ui/toast";
import {
  listUserFeatureOverrides,
  listUsers,
  setUserFeatureOverride,
  updateUserRole,
} from "@/lib/api/admin-users";
import { listFeatureFlags } from "@/lib/api/feature-flags";
import { apiErrorMessage } from "@/lib/api/fetcher";
import { UserRole } from "@/lib/types/enums";
import type { AdminUserListItem } from "@/lib/types/dtos";
import { useEnumLabels } from "@/lib/i18n/enum-labels";
import { useT } from "@/lib/i18n";
import { enumOptions } from "@/lib/enum-options";
import { qk } from "@/lib/query-keys";
import { cn } from "@/lib/utils";

const OVERRIDE_INHERIT = "inherit";
const OVERRIDE_ON = "on";
const OVERRIDE_OFF = "off";

function RoleSelect({ user, isOnlyAdmin }: { user: AdminUserListItem; isOnlyAdmin: boolean }) {
  const t = useT();
  const { UserRoleLabel } = useEnumLabels();
  const queryClient = useQueryClient();
  const [pendingRole, setPendingRole] = useState<number | null>(null);

  const mutate = useMutation({
    mutationFn: (role: number) => updateUserRole(user.id, role),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: qk.adminUsersAll() });
      showToast({ title: t("admin.roleUpdated"), variant: "success" });
    },
    onError: (err) =>
      showToast({
        title: t("admin.roleUpdateFailed"),
        description: apiErrorMessage(err),
        variant: "error",
      }),
  });

  function handleValueChange(v: string) {
    const nextRole = Number(v);
    if (nextRole === user.role) return;

    if (user.role === UserRole.admin && nextRole === UserRole.user && isOnlyAdmin) {
      showToast({ title: t("admin.lastAdminError"), variant: "error" });
      return;
    }

    setPendingRole(nextRole);
  }

  return (
    <>
      <Select
        value={String(user.role)}
        disabled={mutate.isPending}
        onValueChange={handleValueChange}
      >
        <SelectTrigger
          className={cn(
            "w-32",
            user.role === UserRole.admin && "border-primary/40 bg-primary/10 font-medium text-primary",
          )}
        >
          <SelectValue />
        </SelectTrigger>
        <SelectContent>
          {enumOptions(UserRoleLabel).map((o) => (
            <SelectItem key={o.value} value={o.value}>
              {o.label}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>

      <ConfirmDialog
        open={pendingRole !== null}
        onOpenChange={(open) => {
          if (!open) setPendingRole(null);
        }}
        tone="question"
        title={t("admin.roleChangeConfirmTitle")}
        description={
          pendingRole !== null
            ? t("admin.roleChangeConfirmDescription", {
                email: user.email,
                from: UserRoleLabel[user.role] ?? "",
                to: UserRoleLabel[pendingRole] ?? "",
              })
            : undefined
        }
        onConfirm={async () => {
          if (pendingRole === null) return;
          await mutate.mutateAsync(pendingRole);
          setPendingRole(null);
        }}
      />
    </>
  );
}

function FeatureOverrideRow({
  userId,
  featureKey,
  featureLabel,
  currentOverride,
}: {
  userId: string;
  featureKey: string;
  featureLabel: string;
  currentOverride: boolean | undefined;
}) {
  const t = useT();
  const queryClient = useQueryClient();
  const value =
    currentOverride === undefined ? OVERRIDE_INHERIT : currentOverride ? OVERRIDE_ON : OVERRIDE_OFF;

  const mutate = useMutation({
    mutationFn: (next: string) =>
      setUserFeatureOverride(
        userId,
        featureKey,
        next === OVERRIDE_INHERIT ? null : next === OVERRIDE_ON,
      ),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["admin", "user-features", userId] });
      showToast({ title: t("admin.featureOverrideSaved"), variant: "success" });
    },
    onError: (err) =>
      showToast({
        title: t("admin.featureOverrideSaveFailed"),
        description: apiErrorMessage(err),
        variant: "error",
      }),
  });

  return (
    <div className="flex items-center justify-between gap-4">
      <p className="text-sm">{featureLabel}</p>
      <Select value={value} disabled={mutate.isPending} onValueChange={(v) => mutate.mutate(v)}>
        <SelectTrigger className="w-56">
          <SelectValue />
        </SelectTrigger>
        <SelectContent>
          <SelectItem value={OVERRIDE_INHERIT}>{t("admin.featureInherit")}</SelectItem>
          <SelectItem value={OVERRIDE_ON}>{t("admin.featureOn")}</SelectItem>
          <SelectItem value={OVERRIDE_OFF}>{t("admin.featureOff")}</SelectItem>
        </SelectContent>
      </Select>
    </div>
  );
}

function FeaturesDialog({ user }: { user: AdminUserListItem }) {
  const t = useT();
  const [open, setOpen] = useState(false);
  const flags = useQuery({ queryKey: qk.featureFlags(), queryFn: listFeatureFlags });
  const overrides = useQuery({
    queryKey: ["admin", "user-features", user.id],
    queryFn: () => listUserFeatureOverrides(user.id),
    enabled: open,
  });

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <Button variant="outline" size="sm" onClick={() => setOpen(true)}>
        {t("admin.featuresButton")}
      </Button>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{t("admin.featuresDialogTitle", { email: user.email })}</DialogTitle>
          <DialogDescription>{t("admin.featuresDialogDescription")}</DialogDescription>
        </DialogHeader>
        {flags.isPending || overrides.isPending ? (
          <div className="space-y-3">
            <Skeleton className="h-9 w-full" />
            <Skeleton className="h-9 w-full" />
          </div>
        ) : (
          <div className="space-y-4">
            {(flags.data ?? []).map((flag) => (
              <FeatureOverrideRow
                key={flag.key}
                userId={user.id}
                featureKey={flag.key}
                featureLabel={flag.key}
                currentOverride={overrides.data?.find((o) => o.featureKey === flag.key)?.enabled}
              />
            ))}
          </div>
        )}
      </DialogContent>
    </Dialog>
  );
}

export function AdminUsersPage() {
  const t = useT();
  const users = useQuery({ queryKey: qk.adminUsers(1, 200), queryFn: () => listUsers(1, 200) });
  const adminCount = users.data?.items.filter((u) => u.role === UserRole.admin).length ?? 0;

  return (
    <PageShell
      title={t("admin.usersTitle")}
      description={t("admin.usersDescription")}
      actions={
        <div className="flex gap-2">
          <Button variant="outline" size="sm" asChild>
            <Link href="/admin/llm-providers">{t("admin.linkAiProviders")}</Link>
          </Button>
          <Button variant="outline" size="sm" asChild>
            <Link href="/admin/questions/import">{t("admin.linkSeedImport")}</Link>
          </Button>
        </div>
      }
    >
      <div className="space-y-8">
        <GlobalFeatureToggles />

        {users.isPending ? (
          <div className="space-y-3">
            <Skeleton className="h-10 w-full" />
            <Skeleton className="h-10 w-full" />
            <Skeleton className="h-10 w-full" />
          </div>
        ) : users.isError ? (
          <ErrorBanner error={users.error} />
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>{t("admin.table.user")}</TableHead>
                <TableHead>{t("admin.table.role")}</TableHead>
                <TableHead>{t("admin.table.joined")}</TableHead>
                <TableHead>{t("admin.table.lastLogin")}</TableHead>
                <TableHead>{t("admin.table.features")}</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {users.data!.items.map((user) => (
                <TableRow key={user.id}>
                  <TableCell>
                    <div className="min-w-0">
                      <p className="truncate font-medium">{user.displayName ?? user.username}</p>
                      <p className="truncate text-xs text-muted-foreground">{user.email}</p>
                    </div>
                  </TableCell>
                  <TableCell>
                    <RoleSelect
                      user={user}
                      isOnlyAdmin={user.role === UserRole.admin && adminCount <= 1}
                    />
                  </TableCell>
                  <TableCell className="text-sm text-muted-foreground">
                    {new Date(user.createdAt).toLocaleDateString()}
                  </TableCell>
                  <TableCell className="text-sm text-muted-foreground">
                    {user.lastLoginAt
                      ? new Date(user.lastLoginAt).toLocaleDateString()
                      : t("admin.never")}
                  </TableCell>
                  <TableCell>
                    <FeaturesDialog user={user} />
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
      </div>
    </PageShell>
  );
}
