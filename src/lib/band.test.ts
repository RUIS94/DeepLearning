import { describe, expect, it } from "vitest";
import { bandLabel, bandToColor, formatDate } from "./band";

describe("bandToColor", () => {
  it("maps each Band 1-5 to its dedicated CSS variable", () => {
    expect(bandToColor(1)).toBe("var(--band-1)");
    expect(bandToColor(5)).toBe("var(--band-5)");
  });

  it("clamps out-of-range values instead of returning an undefined variable", () => {
    expect(bandToColor(0)).toBe("var(--band-1)");
    expect(bandToColor(9)).toBe("var(--band-5)");
  });

  it("rounds fractional bands", () => {
    expect(bandToColor(2.6)).toBe("var(--band-3)");
  });
});

describe("bandLabel", () => {
  it("labels Band 1 as excellent and Band 5 as weak", () => {
    expect(bandLabel(1)).toBe("优秀");
    expect(bandLabel(5)).toBe("薄弱");
  });

  it("clamps out-of-range bands the same way bandToColor does", () => {
    expect(bandLabel(-3)).toBe(bandLabel(1));
    expect(bandLabel(99)).toBe(bandLabel(5));
  });
});

describe("formatDate", () => {
  it("formats an ISO timestamp as YYYY-MM-DD", () => {
    expect(formatDate("2026-08-31T09:00:00Z")).toBe("2026-08-31");
  });

  it("returns an em dash for null/undefined", () => {
    expect(formatDate(null)).toBe("—");
    expect(formatDate(undefined)).toBe("—");
  });

  it("returns the original string when it cannot be parsed as a date", () => {
    expect(formatDate("not-a-date")).toBe("not-a-date");
  });
});
