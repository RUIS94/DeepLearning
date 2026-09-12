import { describe, expect, it } from "vitest";
import {
  changePasswordSchema,
  forgotPasswordSchema,
  registerSchema,
  resetPasswordSchema,
} from "./auth";

describe("registerSchema", () => {
  it("accepts a matching password/confirmPassword pair", () => {
    expect(
      registerSchema.safeParse({
        email: "a@example.com",
        password: "secret1",
        confirmPassword: "secret1",
      }).success,
    ).toBe(true);
  });

  it("rejects an invalid email", () => {
    expect(
      registerSchema.safeParse({
        email: "not-an-email",
        password: "secret1",
        confirmPassword: "secret1",
      }).success,
    ).toBe(false);
  });

  it("rejects a password under 6 characters", () => {
    expect(
      registerSchema.safeParse({ email: "a@example.com", password: "abc", confirmPassword: "abc" })
        .success,
    ).toBe(false);
  });

  it("rejects a mismatched confirmPassword, flagging that field", () => {
    const result = registerSchema.safeParse({
      email: "a@example.com",
      password: "secret1",
      confirmPassword: "secret2",
    });
    expect(result.success).toBe(false);
    if (!result.success) {
      expect(result.error.issues[0]?.path).toEqual(["confirmPassword"]);
    }
  });
});

describe("forgotPasswordSchema", () => {
  it("requires a valid email", () => {
    expect(forgotPasswordSchema.safeParse({ email: "" }).success).toBe(false);
    expect(forgotPasswordSchema.safeParse({ email: "a@example.com" }).success).toBe(true);
  });
});

describe("resetPasswordSchema", () => {
  it("rejects mismatched passwords", () => {
    expect(
      resetPasswordSchema.safeParse({ password: "secret1", confirmPassword: "secret2" }).success,
    ).toBe(false);
  });
});

describe("changePasswordSchema", () => {
  it("requires a non-empty current password", () => {
    expect(
      changePasswordSchema.safeParse({
        currentPassword: "",
        newPassword: "secret1",
        confirmPassword: "secret1",
      }).success,
    ).toBe(false);
  });

  it("accepts a valid change", () => {
    expect(
      changePasswordSchema.safeParse({
        currentPassword: "oldpass",
        newPassword: "secret1",
        confirmPassword: "secret1",
      }).success,
    ).toBe(true);
  });
});
