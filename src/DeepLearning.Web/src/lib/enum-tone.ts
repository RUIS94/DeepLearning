import { Difficulty, OverrideStatus } from "@/lib/types/enums";

/**
 * "Which semantic color does this enum value read as" — shared status/difficulty → tone mappings
 * that were hand-copied across a couple of components (代码复用扫描_07_优化计划.md §4.9/R-W-5).
 * Only the mappings that were genuinely duplicated (verified byte-identical or near-identical)
 * are here — a couple of other "Xxx → color" spots in the app turned out to be one-off, not
 * duplicated anywhere else, so they were left as they are rather than forced into this file.
 */

/** difficulty-badge.tsx's badge treatment (bg+text, "20"/"foreground" on the warning tone
 * specifically — not a uniform bg-X/12 text-X template, hence a dedicated function rather than a
 * generic tone name + shared template). */
export function difficultyBadgeClass(difficulty: number): string {
  switch (difficulty) {
    case Difficulty.easy:
      return "bg-success/12 text-success";
    case Difficulty.medium:
      return "bg-warning/20 text-warning-foreground";
    case Difficulty.hard:
      return "bg-destructive/12 text-destructive";
    default:
      return "";
  }
}

/** question-card.tsx's bare-text treatment of the same 3 tones. */
export function difficultyTextClass(difficulty: number): string {
  switch (difficulty) {
    case Difficulty.easy:
      return "text-success";
    case Difficulty.medium:
      return "text-warning-foreground";
    case Difficulty.hard:
      return "text-destructive";
    default:
      return "";
  }
}

/** Byte-identical between standard-overrides-panel.tsx and override-detail-page.tsx. */
export const overrideStatusTone: Record<number, string> = {
  [OverrideStatus.observing]: "bg-warning/20 text-warning-foreground",
  [OverrideStatus.active]: "bg-success/12 text-success",
  [OverrideStatus.deprecated]: "bg-muted text-muted-foreground",
};
