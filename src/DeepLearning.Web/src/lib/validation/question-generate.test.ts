import { describe, expect, it } from "vitest";
import { generateQuestionFormSchema } from "./question-generate";

const base = {
  examTypeId: "exam-naati-ct",
  taskType: 0,
  difficulty: 1,
  categoryId: "cat-health",
  targetWeakPoints: false,
};

describe("generateQuestionFormSchema", () => {
  it("accepts a request with no seed questions", () => {
    expect(generateQuestionFormSchema.safeParse({ ...base, seedQuestionIds: null }).success).toBe(
      true,
    );
  });

  it("accepts up to 5 seed questions", () => {
    const result = generateQuestionFormSchema.safeParse({
      ...base,
      seedQuestionIds: ["q-1", "q-2", "q-3", "q-4", "q-5"],
    });
    expect(result.success).toBe(true);
  });

  it("rejects more than 5 seed questions (mirrors backend GenerateQuestionCommand rule)", () => {
    const result = generateQuestionFormSchema.safeParse({
      ...base,
      seedQuestionIds: ["q-1", "q-2", "q-3", "q-4", "q-5", "q-6"],
    });
    expect(result.success).toBe(false);
  });

  it("rejects duplicate seed question ids", () => {
    const result = generateQuestionFormSchema.safeParse({
      ...base,
      seedQuestionIds: ["q-1", "q-1"],
    });
    expect(result.success).toBe(false);
  });

  it("rejects an empty-string seed id", () => {
    const result = generateQuestionFormSchema.safeParse({
      ...base,
      seedQuestionIds: [""],
    });
    expect(result.success).toBe(false);
  });
});
