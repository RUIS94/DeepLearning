import { describe, expect, it } from "vitest";
import {
  findOverlappingAnnotations,
  taskAContentSchema,
  taskBAnnotationSchema,
  taskBContentSchema,
} from "./submission";

describe("taskAContentSchema", () => {
  it("accepts a non-empty translation", () => {
    expect(taskAContentSchema.safeParse("译文内容").success).toBe(true);
  });

  it("rejects an empty or whitespace-only translation", () => {
    expect(taskAContentSchema.safeParse("").success).toBe(false);
    expect(taskAContentSchema.safeParse("   ").success).toBe(false);
  });
});

describe("taskBAnnotationSchema", () => {
  const base = {
    positionStart: 0,
    positionEnd: 4,
    errorCategory: "distortion",
    correctedText: "改正后",
  };

  it("accepts a well-formed annotation", () => {
    expect(taskBAnnotationSchema.safeParse(base).success).toBe(true);
  });

  it("rejects positionEnd <= positionStart", () => {
    const result = taskBAnnotationSchema.safeParse({ ...base, positionEnd: 0 });
    expect(result.success).toBe(false);
  });

  it("rejects an empty errorCategory or correctedText", () => {
    expect(taskBAnnotationSchema.safeParse({ ...base, errorCategory: "" }).success).toBe(false);
    expect(taskBAnnotationSchema.safeParse({ ...base, correctedText: "" }).success).toBe(false);
  });
});

describe("taskBContentSchema", () => {
  it("requires at least one annotation", () => {
    expect(taskBContentSchema.safeParse([]).success).toBe(false);
  });

  it("accepts a non-empty array of valid annotations", () => {
    const result = taskBContentSchema.safeParse([
      { positionStart: 0, positionEnd: 4, errorCategory: "distortion", correctedText: "x" },
    ]);
    expect(result.success).toBe(true);
  });
});

describe("findOverlappingAnnotations", () => {
  it("returns null when ranges do not overlap", () => {
    expect(
      findOverlappingAnnotations([
        { positionStart: 0, positionEnd: 4 },
        { positionStart: 4, positionEnd: 8 },
      ]),
    ).toBeNull();
  });

  it("detects overlapping ranges regardless of input order", () => {
    const overlap = findOverlappingAnnotations([
      { positionStart: 5, positionEnd: 10 },
      { positionStart: 0, positionEnd: 6 },
    ]);
    expect(overlap).not.toBeNull();
  });
});
