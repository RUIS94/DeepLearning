"use client";

import { useEffect } from "react";
import { useRouter } from "next/navigation";
import type { ReactNode } from "react";
import { isAdmin, useCurrentUser } from "@/hooks/use-current-user";
import { Skeleton } from "@/components/ui/skeleton";

/**
 * Role guard for the whole /admin route group (ref/管理员与用户权限隔离_策划书.md §3.3).
 * This is a UX convenience, not the security boundary — every admin endpoint on the backend
 * already 401s/403s on its own (global auth + AdminOnly policy); this only avoids flashing admin
 * screens at a non-admin user before their own API calls start failing.
 */
export default function AdminLayout({ children }: { children: ReactNode }) {
  const router = useRouter();
  const { data: currentUser, isPending } = useCurrentUser();

  useEffect(() => {
    if (isPending) return;
    if (!isAdmin(currentUser)) {
      router.replace("/practice");
    }
  }, [isPending, currentUser, router]);

  if (isPending || !isAdmin(currentUser)) {
    return (
      <div className="space-y-4 p-6">
        <Skeleton className="h-8 w-48" />
        <Skeleton className="h-64 w-full" />
      </div>
    );
  }

  return <>{children}</>;
}
