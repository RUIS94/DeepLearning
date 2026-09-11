/**
 * Turns an enum label map (`Record<number, string>`, e.g. `TaskTypeLabel`) into a
 * `{value,label}[]` options array — the `Object.entries(XLabel).map(([v,l]) => ...)` transform
 * that was hand-copied at ~13 call sites (`CrudField.options`, and inline `<SelectItem>` loops)
 * (代码复用扫描_07_优化计划.md §4.2/R-W-14). `EnumSelect` (components/shared/enum-select.tsx)
 * already does this internally for its own number|"all" value model — use that component directly
 * when it fits; use this function for `CrudField.options` and for the handful of dropdowns whose
 * value model doesn't match EnumSelect (an extra non-"all" sentinel like a "random" option,
 * string-typed rather than numeric form state, etc).
 */
export function enumOptions(labels: Record<number, string>): { value: string; label: string }[] {
  return Object.entries(labels).map(([value, label]) => ({ value, label }));
}
