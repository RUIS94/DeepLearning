<!-- BEGIN:nextjs-agent-rules -->

# This is NOT the Next.js you know

This version has breaking changes — APIs, conventions, and file structure may all differ from your training data. Read the relevant guide in `node_modules/next/dist/docs/` (resolved from this file's directory; in monorepos the `next` package may not be visible from the repo root) before writing any code. Heed deprecation notices.

This block is written and re-added by `next dev` — verify at `node_modules/next/dist/server/lib/generate-agent-files.js`. Removing it from a diff only re-creates the uncommitted change; committing it with your work keeps the tree clean.

<!-- END:nextjs-agent-rules -->

# Full-height page layout (viewport-locked, scroll-inside)

Some pages (答题页 `practice/[questionId]`, 批改页 `submissions/[submissionId]`) lock their
content to the viewport: the page never grows a body scrollbar, the header stays put, and
scrolling happens **inside** the content containers. This is a height chain — every link must
pass the constraint down or it breaks.

## The chain (top → bottom)

1. `src/app/(app)/layout.tsx`
   - `<SidebarProvider className="h-svh min-h-0 overflow-hidden">` — root pinned to the small
     viewport (`h-svh`, no mobile URL-bar jitter); `overflow-hidden` kills body scroll.
   - `<SidebarInset className="min-w-0 overflow-hidden">`
   - `<div className="flex min-h-0 flex-1 flex-col overflow-hidden">` — flex column; `min-h-0`
     lets children shrink below their content size (without it, flex children refuse to shrink
     and the page overflows).

2. `src/components/shell/page-shell.tsx` (used directly and via the `AppShell` compat layer)
   - Outer: `flex min-h-0 flex-1 flex-col`.
   - Header: `shrink-0 px-4 pb-4 pt-6 md:px-6` — natural height, never shrinks.
   - Body: `min-h-0 flex-1 overflow-y-auto px-4 pb-10 pt-6 md:px-6` — eats the remaining
     height and is the fallback scroll container.
   - **Distance from the content area to the browser bottom = the body's `pb-10` = 2.5rem
     (40px).** `pt-6` (24px) is the matching gap under the header. No layout/`SidebarInset`
     margin adds to this.

3. The page component (e.g. `answer-page.tsx`)
   - Grid: `grid gap-6 lg:h-full lg:min-h-0 lg:grid-cols-2`
     - `lg:h-full` — the grid becomes exactly the body's content-box height, so its bottom
       edge lands `pb-10` above the viewport bottom.
     - `lg:min-h-0` — allow it to be constrained rather than grow to content.
   - Each column: `flex min-h-0 flex-col gap-6 lg:overflow-hidden`
     - Grid `align-items: stretch` (default) makes both columns the same height = row height.
     - `lg:overflow-hidden` clips the column so scrolling is delegated to an inner card.
   - The card that should fill the column: `flex min-h-0 flex-1 flex-col` (`shrink-0` on
     `CardHeader`, `min-h-0 flex-1 overflow-y-auto` on `CardContent` — CardContent is the
     real scroll region). Secondary cards in the same column stay `shrink-0` (optionally
     `lg:max-h-[38%] lg:overflow-y-auto`).

Every `lg:` above is intentional: below `lg` none of it applies — the grid collapses to one
column, cards size to content, and the PageShell body is the single scroll container for the
whole page.

## Rules

- Never drop `min-h-0` on a flex item that must shrink. It is the single most common reason
  the chain leaks and a body scrollbar appears.
- The scroll container gets `overflow-y-auto`; its ancestors up to the viewport lock get
  `overflow-hidden`. Only one scroll owner per axis per branch.
- Want the **card itself** to keep the full column height (match the answer page) even when
  its content is short, and center that content: keep the card `flex min-h-0 flex-1 flex-col`
  (it stretches to fill the column) and put the centering wrapper *inside* `CardContent`:

  ```
  <CardContent className="min-h-0 flex-1 overflow-y-auto">
    <div className="flex min-h-full flex-col justify-center gap-4">…</div>
  </CardContent>
  ```

  `min-h-full` keeps the inner wrapper ≥ `CardContent` so `justify-center` centers a short
  payload; when the payload is taller the wrapper grows to content height, `justify-center`
  becomes a no-op, and `CardContent` scrolls with both ends reachable. Do **not** put
  `justify-center` directly on the scroll element (`CardContent`) — it makes the overflowing
  top unreachable. Do **not** center the card within the column instead (`justify-center` on
  the column) — that leaves the card at its small natural height, which is the bug this rule
  fixes.
- For a non-Card fill target (e.g. `submissions/[submissionId]` renders `<GradingResultPanel>`
  when graded), wrap it: `<div className="min-h-0 flex-1 lg:overflow-y-auto"><div
  className="flex min-h-full flex-col justify-center">…</div></div>`.
- A column with **more than one** always-present block: exactly one is the `flex-1` fill card;
  every sibling is `shrink-0`. Any sibling whose content can grow unboundedly (follow-up
  history, a long checkpoint list) also needs its own cap —
  `lg:max-h-[32%] lg:overflow-y-auto` on the card, or `flex min-h-0 flex-col lg:max-h-[40vh]`
  on the card with `min-h-0 overflow-y-auto` on its `CardContent` — or it gets clipped by the
  column's `lg:overflow-hidden`.
