"use client";

import * as React from "react";
import { cn } from "@/lib/utils";

export type AnchorCorner = "top-left" | "top-right" | "bottom-left" | "bottom-right";

export interface DraggablePanelProps extends Omit<
  React.HTMLAttributes<HTMLDivElement>,
  "children"
> {
  /** Offset from the `defaultAnchor` corner, in px. */
  defaultPosition?: { x: number; y: number };
  /**
   * Which corner `defaultPosition` offsets from.
   * Example: `"top-right"` + `{x: 16, y: 16}` means "16px from the top-right".
   */
  defaultAnchor?: AnchorCorner;
  /** If true, position is clamped inside viewport. */
  clampToViewport?: boolean;
  /**
   * Which part of the panel acts as the drag handle:
   *   "content"  — the whole panel is the handle (default).
   *   "children" — children own a `data-draggable-handle` target.
   *               We locate the first such descendant inside the panel and
   *               attach pointer listeners to it.
   *   (A CSS selector string is also accepted for edge cases.)
   */
  handle?: "content" | "children" | string;
  /** Optional drag-cursor override while dragging. */
  draggingCursor?: React.CSSProperties["cursor"];
  children?: React.ReactNode;
}

const clamp = (v: number, min: number, max: number) => Math.max(min, Math.min(max, v));

/**
 * Generic, reusable free-drag floating panel.
 * - Pointer events, works for mouse + touch + pen.
 * - `defaultPosition` is in px, interpreted from top-left.
 * - Position is stored in component state (no persistence across reloads
 *   by design — wrap with a small hook if localStorage is desired later).
 * - Touch-action disabled on the handle to avoid scrolling while dragging.
 */
export function DraggablePanel({
  defaultPosition = { x: 0, y: 0 },
  defaultAnchor = "top-left",
  clampToViewport = true,
  handle = "content",
  draggingCursor = "grabbing",
  className,
  style,
  children,
  ...rest
}: DraggablePanelProps) {
  const panelRef = React.useRef<HTMLDivElement>(null);
  const [pos, setPos] = React.useState<{ x: number; y: number }>(defaultPosition);
  const dragState = React.useRef<{
    startPointerX: number;
    startPointerY: number;
    startX: number;
    startY: number;
    pointerId: number | null;
  }>({
    startPointerX: 0,
    startPointerY: 0,
    startX: defaultPosition.x,
    startY: defaultPosition.y,
    pointerId: null,
  });

  const handleRef = React.useRef<HTMLElement | null>(null);
  const [isDragging, setIsDragging] = React.useState(false);
  const didInitAnchor = React.useRef(false);

  const resolveHandle = React.useCallback(() => {
    if (!panelRef.current) return panelRef.current;
    if (handle === "content") return panelRef.current;
    if (handle === "children") {
      return panelRef.current.querySelector<HTMLElement>("[data-draggable-handle]");
    }
    return panelRef.current.querySelector<HTMLElement>(handle);
  }, [handle]);

  const computePosFromAnchor = React.useCallback(
    (width: number, height: number): { x: number; y: number } => {
      let x = defaultPosition.x;
      let y = defaultPosition.y;
      switch (defaultAnchor) {
        case "top-right":
          x = window.innerWidth - width - defaultPosition.x;
          break;
        case "bottom-left":
          y = window.innerHeight - height - defaultPosition.y;
          break;
        case "bottom-right":
          x = window.innerWidth - width - defaultPosition.x;
          y = window.innerHeight - height - defaultPosition.y;
          break;
        case "top-left":
        default:
          break;
      }
      if (clampToViewport) {
        const maxX = window.innerWidth - (width || 1);
        const maxY = window.innerHeight - (height || 1);
        x = clamp(x, 0, Math.max(0, maxX));
        y = clamp(y, 0, Math.max(0, maxY));
      }
      return { x, y };
    },
    [clampToViewport, defaultAnchor, defaultPosition.x, defaultPosition.y],
  );

  React.useLayoutEffect(() => {
    const el = panelRef.current;
    if (!el || didInitAnchor.current) {
      handleRef.current = resolveHandle() ?? el;
      return;
    }
    const rect = el.getBoundingClientRect();
    if (rect.width > 0 && rect.height > 0) {
      const next = computePosFromAnchor(rect.width, rect.height);
      setPos(next);
      didInitAnchor.current = true;
    }
    handleRef.current = resolveHandle() ?? el;
  }, [computePosFromAnchor, resolveHandle]);

  const onPointerDown = (e: React.PointerEvent<HTMLElement>) => {
    if (!panelRef.current || !handleRef.current) return;
    const handleEl = handleRef.current;
    if (!handleEl.contains(e.target as Node)) return;

    const target = e.target as HTMLElement | null;
    const interactive = target?.closest(
      'button, a, input, textarea, select, [role="button"], [role="link"], [contenteditable="true"]',
    );
    if (interactive && handleEl.contains(interactive as Node)) return;

    e.preventDefault();
    dragState.current = {
      startPointerX: e.clientX,
      startPointerY: e.clientY,
      startX: pos.x,
      startY: pos.y,
      pointerId: e.pointerId,
    };
    setIsDragging(true);
    handleEl.setPointerCapture?.(e.pointerId);
  };

  const onPointerMove = (e: React.PointerEvent<HTMLElement>) => {
    if (dragState.current.pointerId !== e.pointerId) return;
    const { startPointerX, startPointerY, startX, startY } = dragState.current;
    let nextX = startX + (e.clientX - startPointerX);
    let nextY = startY + (e.clientY - startPointerY);

    if (clampToViewport && panelRef.current) {
      const rect = panelRef.current.getBoundingClientRect();
      const maxX = window.innerWidth - (rect.width || 1);
      const maxY = window.innerHeight - (rect.height || 1);
      nextX = clamp(nextX, 0, Math.max(0, maxX));
      nextY = clamp(nextY, 0, Math.max(0, maxY));
    }
    setPos({ x: nextX, y: nextY });
  };

  const endDrag = (e: React.PointerEvent<HTMLElement>) => {
    if (dragState.current.pointerId !== e.pointerId) return;
    dragState.current.pointerId = null;
    setIsDragging(false);
    handleRef.current?.releasePointerCapture?.(e.pointerId);
  };

  return (
    <div
      ref={panelRef}
      data-state={isDragging ? "dragging" : "idle"}
      className={cn("absolute select-none", isDragging && "[touch-action:none]", className)}
      style={{
        left: pos.x,
        top: pos.y,
        cursor: isDragging ? draggingCursor : handle === "content" ? "grab" : undefined,
        ...style,
      }}
      onPointerDown={onPointerDown as unknown as React.PointerEventHandler<HTMLDivElement>}
      onPointerMove={onPointerMove as unknown as React.PointerEventHandler<HTMLDivElement>}
      onPointerUp={endDrag as unknown as React.PointerEventHandler<HTMLDivElement>}
      onPointerCancel={endDrag as unknown as React.PointerEventHandler<HTMLDivElement>}
      {...rest}
    >
      {children}
    </div>
  );
}
