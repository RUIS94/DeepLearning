import { describe, expect, it } from "vitest";
import { errorTaxonomyFormSchema, examTypeFormSchema, promptTemplateFormSchema } from "./admin";

describe("examTypeFormSchema", () => {
  it("accepts a valid snake_case code", () => {
    expect(
      examTypeFormSchema.safeParse({
        code: "naati_ct_en_zh",
        name: "NAATI CT 英译中",
        subjectCategory: 0,
      }).success,
    ).toBe(true);
  });

  it("rejects a code with uppercase letters or spaces", () => {
    expect(
      examTypeFormSchema.safeParse({ code: "NAATI CT", name: "x", subjectCategory: 0 }).success,
    ).toBe(false);
  });
});

describe("errorTaxonomyFormSchema", () => {
  it("rejects an empty categoryKey", () => {
    expect(
      errorTaxonomyFormSchema.safeParse({ categoryKey: "", categoryName: "意义扭曲" }).success,
    ).toBe(false);
  });
});

describe("promptTemplateFormSchema — examTypeId/subjectCategory XOR", () => {
  const base = { templateType: 0, layer: 0, templateContent: "content", version: 1 };

  it("accepts examTypeId alone", () => {
    expect(
      promptTemplateFormSchema.safeParse({ ...base, examTypeId: "exam-1", subjectCategory: -1 })
        .success,
    ).toBe(true);
  });

  it("accepts subjectCategory alone", () => {
    expect(
      promptTemplateFormSchema.safeParse({ ...base, examTypeId: "", subjectCategory: 0 }).success,
    ).toBe(true);
  });

  it("rejects both being set", () => {
    expect(
      promptTemplateFormSchema.safeParse({ ...base, examTypeId: "exam-1", subjectCategory: 0 })
        .success,
    ).toBe(false);
  });

  it("rejects neither being set", () => {
    expect(
      promptTemplateFormSchema.safeParse({ ...base, examTypeId: "", subjectCategory: -1 }).success,
    ).toBe(false);
  });
});
