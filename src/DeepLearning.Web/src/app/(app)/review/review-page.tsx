"use client";

import { useState } from "react";
import { PageShell } from "@/components/shell/page-shell";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { EnumSelect } from "@/components/shared/enum-select";
import { ReviewLibraryList } from "@/components/review/review-library-panel";
import { WeakPointsPanel } from "@/components/review/weak-points-panel";
import { useReviewLibrary } from "@/components/review/use-review-library";
import { useCurrentUser } from "@/hooks/use-current-user";
import { MasteryLevel, MasteryLevelLabel, WeakPointStatusLabel } from "@/lib/types/enums";

const ALL = "all";

export function ReviewPage() {
  const [tab, setTab] = useState("patterns");
  const [mastery, setMastery] = useState(ALL);
  const [domain, setDomain] = useState(ALL);
  const [status, setStatus] = useState<number | "all">("all");

  const currentUser = useCurrentUser();
  const { domains } = useReviewLibrary(currentUser.data?.id);

  const isWeak = tab === "weak-points";

  return (
    <PageShell
      title="复习"
      description="练习中沉淀的句型/词汇复习库，以及 AI 归类的薄弱点。"
      bodyClassName="flex min-h-0 flex-col overflow-hidden"
    >
      <Tabs value={tab} onValueChange={setTab} className="flex min-h-0 flex-1 flex-col">
        <div className="flex shrink-0 flex-wrap items-center justify-between gap-3">
          <TabsList>
            <TabsTrigger value="patterns">句型</TabsTrigger>
            <TabsTrigger value="vocab">词汇表达</TabsTrigger>
            <TabsTrigger value="weak-points">薄弱点</TabsTrigger>
          </TabsList>

          <div className="flex flex-wrap items-center gap-3">
            {isWeak ? (
              <EnumSelect
                labels={WeakPointStatusLabel}
                value={status}
                onChange={setStatus}
                allowAll
                allLabel="全部状态"
                placeholder="状态"
                className="w-36"
              />
            ) : (
              <>
                <Select value={mastery} onValueChange={setMastery}>
                  <SelectTrigger className="w-40">
                    <SelectValue placeholder="掌握程度" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value={ALL}>全部掌握程度</SelectItem>
                    {Object.values(MasteryLevel).map((level) => (
                      <SelectItem key={level} value={String(level)}>
                        {MasteryLevelLabel[level]}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
                <Select value={domain} onValueChange={setDomain}>
                  <SelectTrigger className="w-40">
                    <SelectValue placeholder="题材" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value={ALL}>全部题材</SelectItem>
                    {domains.map((d) => (
                      <SelectItem key={d} value={d}>
                        {d}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </>
            )}
          </div>
        </div>

        <TabsContent value="patterns" className="mt-6 min-h-0 flex-1 overflow-y-auto">
          <ReviewLibraryList kind="patterns" mastery={mastery} domain={domain} />
        </TabsContent>
        <TabsContent value="vocab" className="mt-6 min-h-0 flex-1 overflow-y-auto">
          <ReviewLibraryList kind="vocab" mastery={mastery} domain={domain} />
        </TabsContent>
        <TabsContent value="weak-points" className="mt-6 min-h-0 flex-1 overflow-y-auto">
          <WeakPointsPanel status={status} />
        </TabsContent>
      </Tabs>
    </PageShell>
  );
}
