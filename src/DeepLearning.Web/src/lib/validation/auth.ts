import { z } from "zod";

/**
 * 注册 / 登录 / 忘记密码 / 修改密码这四个表单共用的字段规则——单一定义,避免同一条"密码至少
 * 6 位""两次密码要一致"的校验在四个表单里各写一遍、还容易改一处漏改一处。
 * 6 位下限镜像 Supabase Auth 项目的默认最小密码长度(密码本身从不落到这个仓库自己的库里,
 * 凭证完全由 Supabase Auth 持有,见 User.cs 里 PasswordHash 字段的注释)。
 */
export const emailSchema = z.string().trim().min(1, "v.emailRequired").email("v.emailInvalid");
export const passwordSchema = z.string().min(6, "v.passwordMinLength");

export const registerSchema = z
  .object({
    email: emailSchema,
    password: passwordSchema,
    confirmPassword: z.string(),
  })
  .refine((data) => data.password === data.confirmPassword, {
    message: "v.passwordsMustMatch",
    path: ["confirmPassword"],
  });
export type RegisterFormInput = z.infer<typeof registerSchema>;

export const forgotPasswordSchema = z.object({ email: emailSchema });
export type ForgotPasswordFormInput = z.infer<typeof forgotPasswordSchema>;

export const resetPasswordSchema = z
  .object({ password: passwordSchema, confirmPassword: z.string() })
  .refine((data) => data.password === data.confirmPassword, {
    message: "v.passwordsMustMatch",
    path: ["confirmPassword"],
  });
export type ResetPasswordFormInput = z.infer<typeof resetPasswordSchema>;

export const changePasswordSchema = z
  .object({
    currentPassword: z.string().min(1, "v.currentPasswordRequired"),
    newPassword: passwordSchema,
    confirmPassword: z.string(),
  })
  .refine((data) => data.newPassword === data.confirmPassword, {
    message: "v.passwordsMustMatch",
    path: ["confirmPassword"],
  });
export type ChangePasswordFormInput = z.infer<typeof changePasswordSchema>;
