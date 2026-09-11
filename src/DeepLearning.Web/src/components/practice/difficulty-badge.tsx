"use client";

import { Badge } from "@/components/ui/badge";
import { useEnumLabels } from "@/lib/i18n/enum-labels";
import { cn } from "@/lib/utils";
import { difficultyBadgeClass } from "@/lib/enum-tone";

export function DifficultyBadge({ difficulty }: { difficulty: number }) {
  const { DifficultyLabel } = useEnumLabels();
  return (
    <Badge variant="outline" className={cn("border-transparent", difficultyBadgeClass(difficulty))}>
      {DifficultyLabel[difficulty]}
    </Badge>
  );
}

export function TaskTypeBadge({ taskType }: { taskType: number }) {
  const { TaskTypeLabel } = useEnumLabels();
  return (
    <Badge variant="outline" className="border-border bg-secondary text-secondary-foreground">
      {TaskTypeLabel[taskType]}
    </Badge>
  );
}
