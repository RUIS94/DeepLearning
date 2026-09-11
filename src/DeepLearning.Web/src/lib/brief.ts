/**
 * The brief jsonb column (design doc §6.2: domain/text type/purpose/audience) can show up under
 * either English keys (AI-generated questions) or Chinese keys (user-imported questions, see
 * import-question-panel's form) — this is the single place that knows the mapping, previously
 * split between answer-page.tsx's flat BRIEF_ORDER array (read side) and import-question-panel's
 * BRIEF_FIELDS (write side, Chinese keys only) (代码复用扫描_07_优化计划.md §4.6/R-W-9). The
 * backend's own ParseBriefFields (GenerateQuestionCommandHandler) stays independent — see its
 * comment pointing back here, and this comment pointing there.
 *
 * "topic" has no Chinese counterpart and no import-form field — it's an AI-only key some
 * generated briefs carry. Kept as its own entry so parseBrief still recognizes it.
 */
export const BRIEF_FIELD_ALIASES: { name: string; keys: string[] }[] = [
  { name: "domain", keys: ["domain", "领域"] },
  { name: "topic", keys: ["topic"] },
  { name: "purpose", keys: ["purpose", "目的"] },
  { name: "textType", keys: ["textType", "文本类型"] },
  { name: "audience", keys: ["audience", "受众"] },
];

/** Chinese key each canonical field name is written under — the shape import-question-panel's
 * form submits in (mirrors the backend's expectation for a manually-imported question). */
export const BRIEF_FIELD_KEY: Record<string, string> = {};
for (const f of BRIEF_FIELD_ALIASES) {
  if (f.keys.length > 1) {
    BRIEF_FIELD_KEY[f.name] = f.keys[1]!;
  }
}

const BRIEF_KEY_ORDER = BRIEF_FIELD_ALIASES.flatMap((f) => f.keys);

/**
 * Parses a brief JSON string into ordered `{label, value}` pairs for display — non-empty fields
 * only, in BRIEF_FIELD_ALIASES order regardless of which key variant (English/Chinese) the JSON
 * actually used. `labels` maps a raw JSON key to its display label, supplied by the caller for
 * the current UI language.
 */
export function parseBrief(
  brief: string | null,
  labels: Record<string, string>,
): { label: string; value: string }[] | null {
  if (!brief) return null;
  let parsed: unknown;
  try {
    parsed = JSON.parse(brief);
  } catch {
    return null;
  }
  if (!parsed || typeof parsed !== "object" || Array.isArray(parsed)) return null;
  const rank = (k: string) => {
    const i = BRIEF_KEY_ORDER.indexOf(k);
    return i === -1 ? BRIEF_KEY_ORDER.length : i;
  };
  const entries = Object.entries(parsed as Record<string, unknown>)
    .filter(([, v]) => v != null && String(v).trim() !== "")
    .sort(([a], [b]) => rank(a) - rank(b))
    .map(([k, v]) => ({ label: labels[k] ?? k, value: String(v) }));
  return entries.length ? entries : null;
}

/** Builds the brief JSON string from the import form's fields (Chinese keys, non-empty only);
 * null when every field is blank. */
export function buildBrief(
  fields: Partial<Record<string, string | undefined>> | undefined,
): string | null {
  const obj: Record<string, string> = {};
  for (const [name, key] of Object.entries(BRIEF_FIELD_KEY)) {
    const v = fields?.[name]?.trim();
    if (v) obj[key] = v;
  }
  return Object.keys(obj).length ? JSON.stringify(obj) : null;
}
