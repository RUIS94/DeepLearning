import { describe, expect, it } from "vitest";
import { tFormError } from "./index";

/**
 * `tFormError` is the bridge between the zod schemas' custom `v.*` message keys and the
 * translation function — used at every form error display point (`crud-table`,
 * `import-question-panel`). Built-in zod messages must pass through untouched; `v.*` keys get
 * translated, and a `v.key::<value>` encoding must be split into a `len` param.
 */
const fakeT = ((key: string, params?: Record<string, string>) =>
  params ? `${key}[len=${params["len"]}]` : key) as never;

describe("tFormError", () => {
  it("returns undefined unchanged", () => {
    expect(tFormError(fakeT, undefined)).toBeUndefined();
  });

  it("passes a zod built-in message straight through", () => {
    expect(tFormError(fakeT, "String must contain at least 1 character(s)")).toBe(
      "String must contain at least 1 character(s)",
    );
  });

  it("translates a bare v. key", () => {
    expect(tFormError(fakeT, "v.codeLowerSnake")).toBe("v.codeLowerSnake");
  });

  it("splits a v.key::<value> encoding into a len param", () => {
    expect(tFormError(fakeT, "v.rangeWithinLength::120")).toBe("v.rangeWithinLength[len=120]");
  });
});
