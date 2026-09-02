"use client";

import { Area, AreaChart, CartesianGrid, XAxis, YAxis } from "recharts";
import {
  ChartContainer,
  ChartTooltip,
  ChartTooltipContent,
  type ChartConfig,
} from "@/components/ui/chart";
import type { ProgressSnapshot } from "@/lib/types/dtos";

const config = {
  passRate: { label: "通过率", color: "var(--chart-1)" },
} satisfies ChartConfig;

export function PassRateChart({ snapshots }: { snapshots: ProgressSnapshot[] }) {
  const data = snapshots.map((s) => ({
    period: s.periodEnd,
    passRate: s.passRate === null ? null : Math.round(s.passRate * 100),
  }));

  return (
    <ChartContainer config={config} className="aspect-auto h-56 w-full">
      <AreaChart data={data} margin={{ left: 4, right: 12, top: 8, bottom: 0 }}>
        <defs>
          <linearGradient id="fill-pass-rate" x1="0" y1="0" x2="0" y2="1">
            <stop offset="5%" stopColor="var(--color-passRate)" stopOpacity={0.35} />
            <stop offset="95%" stopColor="var(--color-passRate)" stopOpacity={0.02} />
          </linearGradient>
        </defs>
        <CartesianGrid vertical={false} />
        <XAxis dataKey="period" tickLine={false} axisLine={false} tickMargin={8} />
        <YAxis
          domain={[0, 100]}
          tickFormatter={(v) => `${v}%`}
          tickLine={false}
          axisLine={false}
          tickMargin={8}
          width={36}
        />
        <ChartTooltip content={<ChartTooltipContent formatter={(value) => `${value}%`} />} />
        <Area
          type="monotone"
          dataKey="passRate"
          stroke="var(--color-passRate)"
          fill="url(#fill-pass-rate)"
          strokeWidth={2}
        />
      </AreaChart>
    </ChartContainer>
  );
}
