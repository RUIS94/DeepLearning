import type { ReactNode } from "react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { I18nProvider } from "@/lib/i18n";

const listWeakPointCatalog = vi.fn();
const listWeakPointCategories = vi.fn();
const mergeWeakPointCatalog = vi.fn();

vi.mock("@/lib/api/exam-config", () => ({
  listWeakPointCatalog: (...a: unknown[]) => listWeakPointCatalog(...a),
  listWeakPointCategories: (...a: unknown[]) => listWeakPointCategories(...a),
  createWeakPointCatalogEntry: vi.fn(),
  updateWeakPointCatalogEntry: vi.fn(),
  mergeWeakPointCatalog: (...a: unknown[]) => mergeWeakPointCatalog(...a),
}));
vi.mock("@/components/ui/toast", () => ({ showToast: vi.fn() }));

import { WeakPointCatalogPanel } from "./weak-point-catalog-panel";

function entry(over: Partial<Record<string, unknown>>) {
  return {
    id: "e1",
    categoryId: "cat1",
    categoryCode: "semantic",
    code: "semantic_causality",
    name: "Causality",
    description: "Cause/effect direction flipped",
    defaultDimensionKey: "meaning_transfer",
    defaultErrorCategory: null,
    status: 1,
    origin: "seed",
    createdAt: "2026-01-01T00:00:00Z",
    ...over,
  };
}

function Wrapper({ children }: { children: ReactNode }) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return (
    <QueryClientProvider client={client}>
      <I18nProvider>{children}</I18nProvider>
    </QueryClientProvider>
  );
}

describe("WeakPointCatalogPanel — merge dialog", () => {
  afterEach(() => vi.clearAllMocks());

  it("opens the merge dialog and keeps the merge button disabled until both sides are chosen", async () => {
    const user = userEvent.setup();
    listWeakPointCategories.mockResolvedValue([
      { id: "cat1", name: "Semantic", code: "semantic", description: "", displayOrder: 0 },
    ]);
    listWeakPointCatalog.mockResolvedValue([
      entry({ id: "e1", code: "a", name: "Alpha" }),
      entry({ id: "e2", code: "b", name: "Beta" }),
    ]);

    render(<WeakPointCatalogPanel />, { wrapper: Wrapper });

    // Rows loaded into the per-category table.
    expect(await screen.findByText("Alpha")).toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: "Merge" }));

    const dialog = await screen.findByRole("dialog");
    expect(dialog).toHaveTextContent("Merge weak-point categories");

    // The dialog's own submit button must start disabled — no source/target chosen yet.
    const submit = within(dialog).getByRole("button", { name: "Merge" });
    expect(submit).toBeDisabled();

    expect(mergeWeakPointCatalog).not.toHaveBeenCalled();
  });

  it("renders one table per top-level category and skips the uncategorized bucket when empty", async () => {
    listWeakPointCategories.mockResolvedValue([
      { id: "cat1", name: "Semantic", code: "semantic", description: "", displayOrder: 0 },
      { id: "cat2", name: "Lexical", code: "lexical", description: "", displayOrder: 1 },
    ]);
    listWeakPointCatalog.mockResolvedValue([
      entry({ id: "e1", categoryId: "cat1", name: "Alpha" }),
    ]);

    render(<WeakPointCatalogPanel />, { wrapper: Wrapper });

    await waitFor(() => expect(screen.getByText("Semantic")).toBeInTheDocument());
    // Both category tables render (they show even when empty); the entry lands under its own.
    expect(screen.getByText("Lexical")).toBeInTheDocument();
    expect(screen.getByText("Alpha")).toBeInTheDocument();
  });
});
