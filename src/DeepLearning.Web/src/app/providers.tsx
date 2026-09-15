"use client";

import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { useState, type ReactNode } from "react";
import { I18nProvider } from "@/lib/i18n";
import { BackendStatusBanner } from "@/components/shared/backend-status-banner";

export function Providers({ children }: { children: ReactNode }) {
  const [queryClient] = useState(() => new QueryClient());
  return (
    <QueryClientProvider client={queryClient}>
      <I18nProvider>
        <BackendStatusBanner />
        {children}
      </I18nProvider>
    </QueryClientProvider>
  );
}
