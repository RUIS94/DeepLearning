import { describe, expect, it } from "vitest";
import { importUserQuestionSchema } from "./question-import";

const baseTaskA = {
  taskType: 0,
  difficulty: 1,
  title: "标题",
  sourceText: "Some source text.",
};

const seedError = (start: number, end: number) => ({
  positionStart: start,
  positionEnd: end,
  errorTaxonomyId: "tax-1",
  correctReferenceText: "修正后的文本",
});

describe("importUserQuestionSchema — TaskA", () => {
  it("does not require taskB for TaskA", () => {
    expect(importUserQuestionSchema.safeParse(baseTaskA).success).toBe(true);
  });

  it("rejects an empty title or sourceText", () => {
    expect(importUserQuestionSchema.safeParse({ ...baseTaskA, title: "" }).success).toBe(false);
    expect(importUserQuestionSchema.safeParse({ ...baseTaskA, sourceText: "" }).success).toBe(
      false,
    );
  });
});

describe("importUserQuestionSchema — TaskB (mirrors ImportUserQuestionValidator)", () => {
  const flawed = "0123456789";

  it("rejects TaskB with no taskB payload at all", () => {
    const result = importUserQuestionSchema.safeParse({ ...baseTaskA, taskType: 1 });
    expect(result.success).toBe(false);
  });

  it("rejects TaskB with zero seeded errors", () => {
    const result = importUserQuestionSchema.safeParse({
      ...baseTaskA,
      taskType: 1,
      taskB: { flawedTranslationText: flawed, seededErrors: [] },
    });
    expect(result.success).toBe(false);
  });

  it("accepts a well-formed TaskB payload", () => {
    const result = importUserQuestionSchema.safeParse({
      ...baseTaskA,
      taskType: 1,
      taskB: { flawedTranslationText: flawed, seededErrors: [seedError(0, 3)] },
    });
    expect(result.success).toBe(true);
  });

  it("rejects positionStart >= positionEnd", () => {
    const result = importUserQuestionSchema.safeParse({
      ...baseTaskA,
      taskType: 1,
      taskB: { flawedTranslationText: flawed, seededErrors: [seedError(4, 2)] },
    });
    expect(result.success).toBe(false);
  });

  it("rejects a seeded error range that overruns flawedTranslationText.length", () => {
    const result = importUserQuestionSchema.safeParse({
      ...baseTaskA,
      taskType: 1,
      taskB: { flawedTranslationText: flawed, seededErrors: [seedError(8, 20)] },
    });
    expect(result.success).toBe(false);
  });

  it("rejects overlapping seeded error ranges", () => {
    const result = importUserQuestionSchema.safeParse({
      ...baseTaskA,
      taskType: 1,
      taskB: { flawedTranslationText: flawed, seededErrors: [seedError(0, 5), seedError(3, 7)] },
    });
    expect(result.success).toBe(false);
  });

  it("accepts adjacent (non-overlapping) seeded error ranges", () => {
    const result = importUserQuestionSchema.safeParse({
      ...baseTaskA,
      taskType: 1,
      taskB: { flawedTranslationText: flawed, seededErrors: [seedError(0, 5), seedError(5, 10)] },
    });
    expect(result.success).toBe(true);
  });
});
