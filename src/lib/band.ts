/** Band 1（最好）→ Band 5（最差）的统一视觉编码，图表与徽标共用。 */
export function bandToColor(band: number): string {
  const clamped = Math.min(5, Math.max(1, Math.round(band)));
  return `var(--band-${clamped})`;
}

export function bandLabel(band: number): string {
  const map: Record<number, string> = {
    1: "优秀",
    2: "合格",
    3: "临界",
    4: "偏弱",
    5: "薄弱",
  };
  return map[Math.min(5, Math.max(1, Math.round(band)))] ?? "";
}

export function formatDate(value: string | null | undefined): string {
  if (!value) return "—";
  const d = new Date(value);
  if (Number.isNaN(d.getTime())) return value;
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-${String(d.getDate()).padStart(2, "0")}`;
}
