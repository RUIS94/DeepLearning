import type { ReactNode } from "react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { act, renderHook, waitFor } from "@testing-library/react";

const generateQuestion = vi.fn();
const listQuestions = vi.fn();
const listCategories = vi.fn();
const listWeakPoints = vi.fn();

vi.mock("@/lib/api/questions", () => ({
  generateQuestion: (...a: unknown[]) => generateQuestion(...a),
  listQuestions: (...a: unknown[]) => listQuestions(...a),
}));
vi.mock("@/lib/api/exam-config", () => ({
  listCategories: (...a: unknown[]) => listCategories(...a),
}));
vi.mock("@/lib/api/weak-points", () => ({
  listWeakPoints: (...a: unknown[]) => listWeakPoints(...a),
}));
vi.mock("@/hooks/use-exam-config", () => ({
  useExamType: () => ({ data: { id: "et-1" } }),
}));
vi.mock("@/hooks/use-current-user", () => ({
  useCurrentUser: () => ({ data: { id: "u-1" } }),
}));

import { RANDOM, useAiGenerate } from "./use-ai-generate";

function wrapper({ children }: { children: ReactNode }) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return <QueryClientProvider client={client}>{children}</QueryClientProvider>;
}

function setup(onGenerated = vi.fn()) {
  listQuestions.mockResolvedValue([]);
  listWeakPoints.mockResolvedValue([]);
  listCategories.mockResolvedValue([
    { id: "c1", name: "Legal" },
    { id: "c2", name: "Health" },
  ]);
  generateQuestion.mockResolvedValue({ id: "q-new" });
  const utils = renderHook(() => useAiGenerate(onGenerated), { wrapper });
  return { ...utils, onGenerated };
}

describe("useAiGenerate", () => {
  afterEach(() => {
    vi.clearAllMocks();
  });

  it("resolves RANDOM difficulty and category to real values at submit time", async () => {
    const { result } = setup();
    await waitFor(() => expect(result.current.categories.data).toHaveLength(2));

    expect(result.current.state.difficulty).toBe(RANDOM);
    expect(result.current.state.categoryId).toBe(RANDOM);

    await act(async () => {
      await result.current.mutation.mutateAsync();
    });

    expect(generateQuestion).toHaveBeenCalledTimes(1);
    const req = generateQuestion.mock.calls[0]![0];
    expect(req.examTypeId).toBe("et-1");
    expect(req.taskType).toBe(0);
    expect([0, 1, 2]).toContain(req.difficulty);
    expect(["c1", "c2"]).toContain(req.categoryId);
  });

  it("caps selected seed ids at 5", async () => {
    const { result } = setup();

    act(() => {
      for (const id of ["s1", "s2", "s3", "s4", "s5", "s6", "s7"]) {
        result.current.toggleSeed(id);
      }
    });

    expect(result.current.state.seedIds).toHaveLength(5);
    expect(result.current.state.seedIds).toEqual(["s1", "s2", "s3", "s4", "s5"]);
  });

  it("toggleSeed removes an already-selected id", async () => {
    const { result } = setup();

    act(() => {
      result.current.toggleSeed("s1");
      result.current.toggleSeed("s2");
      result.current.toggleSeed("s1");
    });

    expect(result.current.state.seedIds).toEqual(["s2"]);
  });

  it("resets the form and calls onGenerated after a successful generation", async () => {
    const { result, onGenerated } = setup();
    await waitFor(() => expect(result.current.categories.data).toHaveLength(2));

    act(() => {
      result.current.set("taskType", "1");
      result.current.toggleSeed("s1");
    });
    expect(result.current.state.taskType).toBe("1");

    await act(async () => {
      await result.current.mutation.mutateAsync();
    });

    expect(onGenerated).toHaveBeenCalledWith("q-new");
    expect(result.current.state.taskType).toBe("0");
    expect(result.current.state.seedIds).toEqual([]);
    expect(result.current.state.difficulty).toBe(RANDOM);
  });
});
