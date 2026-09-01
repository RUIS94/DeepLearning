"use client";

import * as React from "react";
import * as DialogPrimitive from "@radix-ui/react-dialog";
import { X } from "lucide-react";
import { cn } from "@/lib/utils";

/**
 * SidePanel —— 从右向中间展开的通用面板（需求里称“popup 右”）。
 *
 * 关键特性(和 CenterModal 的区别):
 * - 高度铺满浏览器视口(h-dvh)。
 * - 非模态(modal={false}):不加遮罩、不锁滚动、不给页面其余部分套 pointer-events:none,
 *   所以打开时仍可选中/复制面板外的正文内容。
 * - 点击外部不会关闭(onInteractOutside 阻止)。只能通过右上角 X、footer 按钮或 Esc 关闭。
 * - 三段式:Header / Body / Footer。Header、Footer 固定不滚动,滚动只发生在 Body。
 *
 * 用法:
 *   <SidePanel open={open} onOpenChange={setOpen}>
 *     <SidePanelContent width="34rem">
 *       <SidePanelHeader title="AI 出题" description="生成是慢请求……" />
 *       <SidePanelBody>{form}</SidePanelBody>
 *       <SidePanelFooter>{buttons}</SidePanelFooter>
 *     </SidePanelContent>
 *   </SidePanel>
 */
function SidePanel(props: React.ComponentProps<typeof DialogPrimitive.Root>) {
  return <DialogPrimitive.Root modal={false} {...props} />;
}

const SidePanelContent = React.forwardRef<
  React.ElementRef<typeof DialogPrimitive.Content>,
  React.ComponentPropsWithoutRef<typeof DialogPrimitive.Content> & {
    /** 面板宽度,默认 32rem。可传 "40rem" / "90vw" 等。 */
    width?: string;
  }
>(({ className, children, width = "32rem", style, ...props }, ref) => (
  <DialogPrimitive.Portal>
    <DialogPrimitive.Content
      ref={ref}
      // 点击面板外部不关闭；外部区域也不被锁定,方便复制内容。
      onInteractOutside={(event) => event.preventDefault()}
      onPointerDownOutside={(event) => event.preventDefault()}
      style={{ width, ...style }}
      className={cn(
        "fixed inset-y-0 right-0 z-50 flex h-dvh max-w-[100vw] flex-col border-l border-border bg-background shadow-2xl",
        "duration-300 data-[state=open]:animate-in data-[state=closed]:animate-out",
        "data-[state=closed]:slide-out-to-right data-[state=open]:slide-in-from-right",
        className,
      )}
      {...props}
    >
      {children}
    </DialogPrimitive.Content>
  </DialogPrimitive.Portal>
));
SidePanelContent.displayName = "SidePanelContent";

function SidePanelHeader({
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

function SidePanelBody({ className, ...props }: React.HTMLAttributes<HTMLDivElement>) {
  return <div className={cn("min-h-0 flex-1 overflow-y-auto px-6 py-5", className)} {...props} />;
}

function SidePanelFooter({ className, ...props }: React.HTMLAttributes<HTMLDivElement>) {
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

export { SidePanel, SidePanelContent, SidePanelHeader, SidePanelBody, SidePanelFooter };
