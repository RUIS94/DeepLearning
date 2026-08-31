"use client";

import Link from "next/link";
import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { AppShell } from "@/components/shared/app-shell";
import { Card, CardContent } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { Skeleton } from "@/components/ui/skeleton";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import {
  listReviewPatterns,
  listReviewVocab,
  reviewPattern,
  reviewVocabItem,
} from "@/lib/api/review-library";
import { useCurrentUser } from "@/hooks/use-current-user";
import { MasteryLevel, MasteryLevelLabel } from "@/lib/types/enums";
import { formatDate } from "@/lib/band";

const ALL = "all";

function MasteryPicker({
  value,
  onChange,
  pending,
}: {
  value: number;
  onChange: (level: number) => void;
  pending: boolean;
}) {
  return (
    <div className="flex gap-1">
      {Object.values(MasteryLevel).map((level) => (
        <Button
          key={level}
          size="sm"
          variant={value === level ? "default" : "outline"}
          disabled={pending}
          onClick={() => onChange(level)}
        >
          {MasteryLevelLabel[level]}
        </Button>
      ))}
    </div>
  );
}

export function ReviewLibraryPage() {
  const queryClient = useQueryClient();
  const [mastery, setMastery] = useState(ALL);
  const [domain, setDomain] = useState(ALL);
  const currentUser = useCurrentUser();
  const userId = currentUser.data?.id;

  const patterns = useQuery({
    queryKey: ["review-patterns", userId],
    queryFn: () => listReviewPatterns(userId!),
    enabled: !!userId,
  });
  const vocab = useQuery({
    queryKey: ["review-vocab", userId],
    queryFn: () => listReviewVocab(userId!),
    enabled: !!userId,
  });

  const markPattern = useMutation({
    mutationFn: (v: { id: string; level: number }) => reviewPattern(userId!, v.id, v.level),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["review-patterns"] }),
  });
  const markVocab = useMutation({
    mutationFn: (v: { id: string; level: number }) => reviewVocabItem(userId!, v.id, v.level),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["review-vocab"] }),
  });

  const domains = Array.from(
    new Set([...(patterns.data ?? []), ...(vocab.data ?? [])].map((i) => i.domain).filter(Boolean)),
  ) as string[];

  const filter = <T extends { masteryLevel: number; domain: string | null }>(items: T[]) =>
    items
      .filter((i) => (mastery === ALL ? true : i.masteryLevel === Number(mastery)))
      .filter((i) => (domain === ALL ? true : i.domain === domain));

  return (
    <AppShell
      title="复习库"
      description="练习中沉淀下来的句型与词汇，点击掌握程度即可记录一次复习。"
    >
      <div className="mb-6 flex flex-wrap gap-3">
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
      </div>

      <Tabs defaultValue="patterns">
        <TabsList>
          <TabsTrigger value="patterns">句型</TabsTrigger>
          <TabsTrigger value="vocab">词汇表达</TabsTrigger>
        </TabsList>

        <TabsContent value="patterns" className="mt-6 space-y-4">
          {patterns.isPending ? (
            <Skeleton className="h-40 w-full rounded-xl" />
          ) : (
            filter(patterns.data ?? []).map((p) => (
              <Card key={p.id} className="border-border shadow-none">
                <CardContent className="flex flex-wrap items-start justify-between gap-4 p-5">
                  <div className="min-w-64 flex-1 space-y-2">
                    <p className="text-sm font-medium">{p.patternName}</p>
                    <p className="source-text text-sm text-muted-foreground">{p.exampleSentence}</p>
                    <div className="flex flex-wrap items-center gap-2">
                      {[p.domain, p.scenario, p.frequencyTag].filter(Boolean).map((t) => (
                        <Badge
                          key={t}
                          variant="outline"
                          className="border-border text-muted-foreground"
                        >
                          {t}
                        </Badge>
                      ))}
                      <span className="text-numeric text-xs text-muted-foreground">
                        遇见 {p.timesEncountered} 次 · 上次复习 {formatDate(p.lastReviewedAt)}
                      </span>
                      {p.questionId ? (
                        <Link
                          href={`/practice/${p.questionId}`}
                          className="text-xs text-primary underline underline-offset-2"
                        >
                          回到原题
                        </Link>
                      ) : null}
                    </div>
                  </div>
                  <MasteryPicker
                    value={p.masteryLevel}
                    pending={markPattern.isPending}
                    onChange={(level) => markPattern.mutate({ id: p.id, level })}
                  />
                </CardContent>
              </Card>
            ))
          )}
        </TabsContent>

        <TabsContent value="vocab" className="mt-6 space-y-4">
          {vocab.isPending ? (
            <Skeleton className="h-40 w-full rounded-xl" />
          ) : (
            filter(vocab.data ?? []).map((v) => (
              <Card key={v.id} className="border-border shadow-none">
                <CardContent className="flex flex-wrap items-start justify-between gap-4 p-5">
                  <div className="min-w-64 flex-1 space-y-2">
                    <p className="text-sm font-medium">{v.englishExpr}</p>
                    <p className="text-sm text-primary">{v.chineseEquiv}</p>
                    <div className="flex flex-wrap items-center gap-2">
                      {[v.domain, v.scenario, v.frequencyTag].filter(Boolean).map((t) => (
                        <Badge
                          key={t}
                          variant="outline"
                          className="border-border text-muted-foreground"
                        >
                          {t}
                        </Badge>
                      ))}
                      <span className="text-numeric text-xs text-muted-foreground">
                        遇见 {v.timesEncountered} 次 · 上次复习 {formatDate(v.lastReviewedAt)}
                      </span>
                      {v.questionId ? (
                        <Link
                          href={`/practice/${v.questionId}`}
                          className="text-xs text-primary underline underline-offset-2"
                        >
                          回到原题
                        </Link>
                      ) : null}
                    </div>
                  </div>
                  <MasteryPicker
                    value={v.masteryLevel}
                    pending={markVocab.isPending}
                    onChange={(level) => markVocab.mutate({ id: v.id, level })}
                  />
                </CardContent>
              </Card>
            ))
          )}
        </TabsContent>
      </Tabs>
    </AppShell>
  );
}
