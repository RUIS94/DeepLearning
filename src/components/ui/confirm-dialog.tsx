"use client";

import * as React from "react";
import * as AlertDialogPrimitive from "@radix-ui/react-alert-dialog";
import { AlertTriangle, HelpCircle, Info, Loader2 } from "lucide-react";
import { cn } from "@/lib/utils";
import { Button } from "@/components/ui/button";

/** 弹窗性质:通知 / 警告 / 询问。决定 icon、icon 颜色与主按钮颜色。 */
export type ConfirmTone = "info" | "warning" | "question";

const TONE: Record<
  ConfirmTone,
  {
    icon: React.ComponentType<{ className?: string }>;
    iconWrap: string;
    confirmVariant: React.ComponentProps<typeof Button>["variant"];
  }
> = {
  info: { icon: Info, iconWrap: "bg-blue-500/10 text-blue-500", confirmVariant: "default" },
  warning: {
    icon: AlertTriangle,
    iconWrap: "bg-destructive/10 text-destructive",
    confirmVariant: "destructive",
  },
  question: { icon: HelpCircle, iconWrap: "bg-primary/10 text-primary", confirmVariant: "default" },
};

/**
 * ConfirmDialog —— 通用确认弹窗（需求里称“确认 popup”）。
 *
 * - tone 决定图标、图标颜色、主按钮颜色。
 * - 主/副按钮文案与点击行为都由调用处传入。
 * - onConfirm 可以是 async:执行期间两个按钮禁用、主按钮转圈;成功后自动关闭,抛错则保持打开
 *   (调用处可自行用 toast 报错)。
 *
 * 用法:
 *   <ConfirmDialog
 *     open={open} onOpenChange={setOpen}
 *     tone="warning" title="退出登录?" description="将结束当前会话。"
 *     confirmLabel="退出" cancelLabel="取消"
 *     onConfirm={async () => { await signOut(); router.push("/"); }}
 *   />
 */
export function ConfirmDialog({
  open,
  onOpenChange,
  tone = "question",
  title,
  description,
  confirmLabel = "确定",
  cancelLabel = "取消",
  onConfirm,
  onCancel,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  tone?: ConfirmTone;
  title: React.ReactNode;
  description?: React.ReactNode;
  confirmLabel?: string;
  cancelLabel?: string;
  onConfirm: () => void | Promise<void>;
  onCancel?: () => void;
}) {
  const [pending, setPending] = React.useState(false);
  const { icon: Icon, iconWrap, confirmVariant } = TONE[tone];

  async function handleConfirm() {
    try {
      setPending(true);
      await onConfirm();
      onOpenChange(false);
    } catch {
      // 保持打开,交给调用处报错
    } finally {
      setPending(false);
    }
  }

  return (
    <AlertDialogPrimitive.Root
      open={open}
      onOpenChange={(next) => {
        if (pending) return;
        if (!next) onCancel?.();
        onOpenChange(next);
      }}
    >
      <AlertDialogPrimitive.Portal>
        <AlertDialogPrimitive.Overlay
          className={cn(
            "fixed inset-0 z-50 bg-black/50",
            "data-[state=open]:animate-in data-[state=closed]:animate-out data-[state=closed]:fade-out-0 data-[state=open]:fade-in-0",
          )}
        />
        <AlertDialogPrimitive.Content
          className={cn(
            "fixed left-1/2 top-1/2 z-50 w-full max-w-md -translate-x-1/2 -translate-y-1/2",
            "rounded-xl border border-border bg-background p-6 shadow-2xl",
            "duration-200 data-[state=open]:animate-in data-[state=closed]:animate-out data-[state=closed]:fade-out-0 data-[state=open]:fade-in-0 data-[state=closed]:zoom-out-95 data-[state=open]:zoom-in-95",
          )}
        >
          <div className="flex gap-4">
            <span
              className={cn(
                "flex size-10 shrink-0 items-center justify-center rounded-full",
                iconWrap,
              )}
            >
              <Icon className="size-5" />
            </span>
            <div className="min-w-0 flex-1 space-y-1.5">
              <AlertDialogPrimitive.Title className="text-base font-semibold leading-tight tracking-tight">
                {title}
              </AlertDialogPrimitive.Title>
              {description ? (
                <AlertDialogPrimitive.Description className="text-sm leading-relaxed text-muted-foreground">
                  {description}
                </AlertDialogPrimitive.Description>
              ) : null}
            </div>
          </div>

          <div className="mt-6 flex items-center justify-end gap-2">
            <AlertDialogPrimitive.Cancel asChild>
              <Button variant="outline" size="sm" disabled={pending}>
                {cancelLabel}
              </Button>
            </AlertDialogPrimitive.Cancel>
            <Button variant={confirmVariant} size="sm" disabled={pending} onClick={handleConfirm}>
              {pending ? <Loader2 className="size-4 animate-spin" /> : null}
              {confirmLabel}
            </Button>
          </div>
        </AlertDialogPrimitive.Content>
      </AlertDialogPrimitive.Portal>
    </AlertDialogPrimitive.Root>
  );
}
