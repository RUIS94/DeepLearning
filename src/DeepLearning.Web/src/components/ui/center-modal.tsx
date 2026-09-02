"use client";

import * as React from "react";
import * as DialogPrimitive from "@radix-ui/react-dialog";
import { X } from "lucide-react";
import { cn } from "@/lib/utils";

/**
 * CenterModal —— 屏幕中央打开的通用模态框（需求里称“popup 中”）。
 *
 * 关键特性(和 SidePanel 的区别):
 * - 模态:带遮罩、锁滚动、外部不可交互。
 * - 宽度由外部传入(width prop)。
 * - 高度响应内容,但不超过视口,且与视口上下留出间距(max-h = 视口 - 4rem)。
 * - 三段式:Header / Body / Footer。Header、Footer 固定不滚动,滚动只发生在 Body。
 *
 * 用法:
 *   <CenterModal open={open} onOpenChange={setOpen}>
 *     <CenterModalContent width="36rem">
 *       <CenterModalHeader title="标题" description="说明" />
 *       <CenterModalBody>{content}</CenterModalBody>
 *       <CenterModalFooter>{buttons}</CenterModalFooter>
 *     </CenterModalContent>
 *   </CenterModal>
 */
const CenterModal = DialogPrimitive.Root;

const CenterModalContent = React.forwardRef<
  React.ElementRef<typeof DialogPrimitive.Content>,
  React.ComponentPropsWithoutRef<typeof DialogPrimitive.Content> & {
    /** 模态框宽度,默认 32rem。 */
    width?: string;
    /** 点击遮罩是否关闭,默认 true。 */
    closeOnOutside?: boolean;
  }
>(({ className, children, width = "32rem", closeOnOutside = true, style, ...props }, ref) => (
  <DialogPrimitive.Portal>
    <DialogPrimitive.Overlay
      className={cn(
        "fixed inset-0 z-50 bg-black/50",
        "data-[state=open]:animate-in data-[state=closed]:animate-out data-[state=closed]:fade-out-0 data-[state=open]:fade-in-0",
      )}
    />
    <DialogPrimitive.Content
      ref={ref}
      {...(closeOnOutside ? {} : { onInteractOutside: (event: Event) => event.preventDefault() })}
      style={{ width, ...style }}
      className={cn(
        "fixed left-1/2 top-1/2 z-50 flex max-h-[calc(100dvh-4rem)] w-full max-w-[calc(100vw-2rem)]",
        "-translate-x-1/2 -translate-y-1/2 flex-col overflow-hidden rounded-xl border border-border bg-background shadow-2xl",
        "duration-200 data-[state=open]:animate-in data-[state=closed]:animate-out data-[state=closed]:fade-out-0 data-[state=open]:fade-in-0 data-[state=closed]:zoom-out-95 data-[state=open]:zoom-in-95",
        className,
      )}
      {...props}
    >
      {children}
    </DialogPrimitive.Content>
  </DialogPrimitive.Portal>
));
CenterModalContent.displayName = "CenterModalContent";

function CenterModalHeader({
  title,
  description,
  className,
  children,
  showClose = true,
}: {
  title: React.ReactNode;
  description?: React.ReactNode;
  className?: string;
  children?: React.ReactNode;
  showClose?: boolean;
}) {
  return (
    <div
      className={cn(
        "flex shrink-0 items-start justify-between gap-4 border-b border-border px-6 py-4",
        className,
      )}
    >
      <div className="min-w-0 space-y-1">
        <DialogPrimitive.Title className="text-base font-semibold leading-tight tracking-tight">
          {title}
        </DialogPrimitive.Title>
        {description ? (
          <DialogPrimitive.Description className="text-sm leading-relaxed text-muted-foreground">
            {description}
          </DialogPrimitive.Description>
        ) : null}
        {children}
      </div>
      {showClose ? (
        <DialogPrimitive.Close
          className="mt-0.5 shrink-0 rounded-md p-1 text-muted-foreground transition-colors hover:bg-secondary hover:text-foreground"
          aria-label="关闭"
        >
          <X className="size-4" />
        </DialogPrimitive.Close>
      ) : null}
    </div>
  );
}

function CenterModalBody({ className, ...props }: React.HTMLAttributes<HTMLDivElement>) {
  return <div className={cn("min-h-0 flex-1 overflow-y-auto px-6 py-5", className)} {...props} />;
}

function CenterModalFooter({ className, ...props }: React.HTMLAttributes<HTMLDivElement>) {
  return (
    <div
      className={cn(
        "flex shrink-0 items-center justify-end gap-2 border-t border-border px-6 py-4",
        className,
      )}
      {...props}
    />
  );
}

export { CenterModal, CenterModalContent, CenterModalHeader, CenterModalBody, CenterModalFooter };
