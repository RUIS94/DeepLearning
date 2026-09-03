"use client";

import { Loader2, Sparkles } from "lucide-react";
import {
  SidePanel,
  SidePanelBody,
  SidePanelContent,
  SidePanelFooter,
  SidePanelHeader,
} from "@/components/ui/side-panel";
import { Button } from "@/components/ui/button";
import { Label } from "@/components/ui/label";
import { Switch } from "@/components/ui/switch";
import { Badge } from "@/components/ui/badge";
import { Checkbox } from "@/components/ui/checkbox";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { ErrorBanner } from "@/components/shared/ai-loading-state";
import { RANDOM, type useAiGenerate } from "@/hooks/use-ai-generate";
import { DifficultyLabel, PriorityLabel, TaskTypeLabel } from "@/lib/types/enums";

/**
 * AI 出题面板(SidePanel / “popup 右”)。表单与提交状态由父组件通过 useAiGenerate 持有,
 * 关掉面板不会重置;生成进行中面板体切换成 loading,footer 禁用,重新打开仍是 loading —— 直到
 * 成功(父组件 onGenerated:toast + 跳答题页 + 关闭)。
 */
export function AiGeneratePanel({
  open,
  onOpenChange,
  gen,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  gen: ReturnType<typeof useAiGenerate>;
}) {
  const { state, set, toggleSeed, mutation, categories, weakPoints, seeds, examTypeReady } = gen;
  const pending = mutation.isPending;

  return (
    <SidePanel open={open} onOpenChange={onOpenChange}>
      <SidePanelContent width="34rem">
        <SidePanelHeader
          title="AI 出题"
          description="题目生成后会直接进入答题页"
        />

        <SidePanelBody>
          {pending ? (
            <div className="flex h-full flex-col items-center justify-center gap-3 py-16 text-center">
              <Loader2 className="size-8 animate-spin text-primary" />
              <p className="text-sm font-medium">AI 正在出题…</p>
              <p className="max-w-xs text-xs text-muted-foreground">
                可能需要几秒到十几秒。可以关掉这个面板去做别的，生成完成会自动通知并进入答题页。
              </p>
            </div>
          ) : (
            <div className="space-y-6">
              <div className="grid gap-4 sm:grid-cols-2">
                <div className="space-y-2">
                  <Label>任务类型</Label>
                  <Select value={state.taskType} onValueChange={(v) => set("taskType", v)}>
                    <SelectTrigger>
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      {Object.entries(TaskTypeLabel).map(([v, l]) => (
                        <SelectItem key={v} value={v}>
                          {l}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
                <div className="space-y-2">
                  <Label>难度</Label>
                  <Select value={state.difficulty} onValueChange={(v) => set("difficulty", v)}>
                    <SelectTrigger>
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value={RANDOM}>随机</SelectItem>
                      {Object.entries(DifficultyLabel).map(([v, l]) => (
                        <SelectItem key={v} value={v}>
                          {l}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
              </div>

              <div className="space-y-2">
                <Label>题材分类</Label>
                <Select value={state.categoryId} onValueChange={(v) => set("categoryId", v)}>
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value={RANDOM}>随机</SelectItem>
                    {(categories.data ?? []).map((c) => (
                      <SelectItem key={c.id} value={c.id}>
                        {c.name}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>

              <div className="space-y-3 rounded-lg border border-border p-4">
                <div className="flex items-start justify-between gap-4">
                  <div>
                    <p className="text-sm font-medium">命中薄弱点</p>
                    <p className="mt-1 text-xs text-muted-foreground">
                      让 AI 优先围绕当前活跃薄弱点设计题目。
                    </p>
                  </div>
                  <Switch
                    checked={state.targetWeakPoints}
                    onCheckedChange={(c) => set("targetWeakPoints", c)}
                  />
                </div>
                {state.targetWeakPoints ? (
                  <div className="space-y-2 border-t border-border pt-3">
                    {weakPoints.isPending ? (
                      <p className="text-xs text-muted-foreground">加载薄弱点…</p>
                    ) : (weakPoints.data ?? []).length === 0 ? (
                      <p className="text-xs text-muted-foreground">当前没有活跃薄弱点。</p>
                    ) : (
                      (weakPoints.data ?? []).map((w) => (
                        <div key={w.id} className="flex items-center justify-between gap-2 text-sm">
                          <span className="min-w-0 truncate">{w.label}</span>
                          <Badge
                            variant="outline"
                            className="shrink-0 border-accent/40 text-accent"
                          >
                            {PriorityLabel[w.priority]}优先
                          </Badge>
                        </div>
                      ))
                    )}
                  </div>
                ) : null}
              </div>

              <div className="space-y-2">
                <Label>真题种子（可选，最多 5 道）</Label>
                {seeds.isPending ? (
                  <p className="text-xs text-muted-foreground">加载真题种子…</p>
                ) : (seeds.data ?? []).length === 0 ? (
                  <p className="text-xs text-muted-foreground">
                    题库里还没有标记为真题种子的题目。
                  </p>
                ) : (
                  <div className="space-y-1.5">
                    {(seeds.data ?? []).map((q) => {
                      const checked = state.seedIds.includes(q.id);
                      const atLimit = state.seedIds.length >= 5 && !checked;
                      return (
                        <label
                          key={q.id}
                          className="flex items-center gap-2.5 rounded-md border border-border px-3 py-2 text-sm has-[:disabled]:opacity-50"
                        >
                          <Checkbox
                            checked={checked}
                            disabled={atLimit}
                            onCheckedChange={() => toggleSeed(q.id)}
                          />
                          <span className="min-w-0 truncate">{q.title}</span>
                        </label>
                      );
                    })}
                    <p className="text-numeric text-xs text-muted-foreground">
                      已选 {state.seedIds.length} / 5
                    </p>
                  </div>
                )}
              </div>

              {mutation.isError ? <ErrorBanner error={mutation.error} /> : null}
            </div>
          )}
        </SidePanelBody>

        <SidePanelFooter>
          <Button variant="outline" size="sm" onClick={() => onOpenChange(false)}>
            取消
          </Button>
          <Button size="sm" disabled={pending || !examTypeReady} onClick={() => mutation.mutate()}>
            {pending ? (
              <Loader2 className="size-4 animate-spin" />
            ) : (
              <Sparkles className="size-4" />
            )}
            {pending ? "生成中…" : "生成题目"}
          </Button>
        </SidePanelFooter>
      </SidePanelContent>
    </SidePanel>
  );
}
