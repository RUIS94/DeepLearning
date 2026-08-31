"use client";

import { CartesianGrid, Line, LineChart, XAxis, YAxis, ReferenceDot } from "recharts";
import {
  ChartContainer,
  ChartLegend,
  ChartLegendContent,
  ChartTooltip,
  ChartTooltipContent,
  type ChartConfig,
} from "@/components/ui/chart";
import type { ProgressSnapshot } from "@/lib/types/dtos";

const config = {
  meaningTransfer: { label: "意义传递", color: "var(--chart-1)" },
  textualNorms: { label: "语篇规范", color: "var(--chart-2)" },
  languageProficiency: { label: "语言能力", color: "var(--chart-3)" },
} satisfies ChartConfig;

export function BandTrendChart({ snapshots }: { snapshots: ProgressSnapshot[] }) {
  const data = snapshots.map((s) => ({
    period: s.periodEnd,
    meaningTransfer: s.avgBandMeaningTransfer,
    textualNorms: s.avgBandTextualNorms,
    languageProficiency: s.avgBandLanguageProficiency,
    keyTurningPoint: s.keyTurningPoint,
  }));

  const turningPoints = data.filter((d) => d.keyTurningPoint && d.meaningTransfer !== null);

  return (
    <ChartContainer config={config} className="aspect-auto h-72 w-full">
      <LineChart data={data} margin={{ left: 4, right: 12, top: 8, bottom: 0 }}>
        <CartesianGrid vertical={false} />
        <XAxis dataKey="period" tickLine={false} axisLine={false} tickMargin={8} />
        <YAxis
          reversed
          domain={[1, 5]}
          ticks={[1, 2, 3, 4, 5]}
          tickLine={false}
          axisLine={false}
          tickMargin={8}
          width={28}
        />
        <ChartTooltip content={<ChartTooltipContent />} />
        <ChartLegend content={<ChartLegendContent />} />
        <Line
          type="monotone"
          dataKey="meaningTransfer"
          stroke="var(--color-meaningTransfer)"
          strokeWidth={2}
          dot={false}
        />
        <Line
          type="monotone"
          dataKey="textualNorms"
          stroke="var(--color-textualNorms)"
          strokeWidth={2}
          dot={false}
        />
        <Line
          type="monotone"
          dataKey="languageProficiency"
          stroke="var(--color-languageProficiency)"
          strokeWidth={2}
          dot={false}
        />
        {turningPoints.map((p) => (
          <ReferenceDot
            key={p.period}
            x={p.period}
            y={p.meaningTransfer!}
            r={5}
            fill="var(--accent)"
            stroke="var(--background)"
            strokeWidth={2}
          />
        ))}
      </LineChart>
    </ChartContainer>
  );
}
