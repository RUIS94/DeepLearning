const TOAST_DURATION_STORAGE_KEY = "dev-toast-duration-ms";
const DEFAULT_TOAST_DURATION_MS = 5000;

export function getToastDuration(): number {
  const stored = Number(localStorage.getItem(TOAST_DURATION_STORAGE_KEY));
  return Number.isFinite(stored) && stored > 0 ? stored : DEFAULT_TOAST_DURATION_MS;
}

export function setToastDuration(valueMs: number) {
  localStorage.setItem(TOAST_DURATION_STORAGE_KEY, String(valueMs));
}
