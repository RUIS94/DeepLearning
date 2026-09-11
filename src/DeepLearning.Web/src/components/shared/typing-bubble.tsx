/**
 * The 3-dot "AI is typing" bouncing bubble — was byte-identical at 2 spots in follow-up-panel.tsx
 * (代码复用扫描_07_优化计划.md §4.3/R-W-15).
 */
export function TypingBubble() {
  return (
    <div className="flex items-center gap-1 rounded-lg bg-muted px-3 py-2.5">
      <span className="size-1.5 animate-bounce rounded-full bg-muted-foreground [animation-delay:-0.3s]" />
      <span className="size-1.5 animate-bounce rounded-full bg-muted-foreground [animation-delay:-0.15s]" />
      <span className="size-1.5 animate-bounce rounded-full bg-muted-foreground" />
    </div>
  );
}
