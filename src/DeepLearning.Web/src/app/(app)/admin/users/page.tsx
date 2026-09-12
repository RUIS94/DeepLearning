import type { Metadata } from "next";
import { AdminUsersPage } from "./admin-users-page";

export const metadata: Metadata = { title: "User Management" };

/** Admin-only (A3/A4, ref/管理员与用户权限隔离_策划书.md) — role guard lives in app/(app)/admin/layout.tsx. */
export default function Page() {
  return <AdminUsersPage />;
}
