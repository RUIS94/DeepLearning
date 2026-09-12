import type { Metadata } from "next";
import { SeedImportPage } from "./seed-import-page";

export const metadata: Metadata = { title: "Import Real-Exam Seed" };

/** Admin-only (ref/管理员与用户权限隔离_策划书.md A2') — role guard lives in app/(app)/admin/layout.tsx. */
export default function Page() {
  return <SeedImportPage />;
}
