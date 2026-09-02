"use client";

import { createContext, useContext, useEffect, useRef, useState, type ComponentType } from "react";
import { AlertCircle, AlertTriangle, CheckCircle2, Info } from "lucide-react";
import { getToastDuration } from "@/lib/devSettings";

// Provided by App.tsx so toasts center within the main content area
// (viewport minus the sidebar) instead of the full viewport width.
export const ToastCenterContext = createContext<number | null>(null);

export type ToastVariant = "success" | "warning" | "error" | "info";

export interface ShowToastOptions {
  title: string;
  description?: string;
  variant?: ToastVariant;
  duration?: number;
  /**
   * Toasts sharing a dedupe key are collapsed into one — a call made while a match is still
   * showing (not yet exiting) is dropped instead of restacking or restarting its timer.
   * Defaults to a key derived from variant/title/description, so identical messages naturally
   * dedupe without callers needing to pass anything.
   */
  dedupeKey?: string;
  onVisibleChange?: (visible: boolean) => void;
}

interface ToastEntry {
  id: number;
  title: string;
  description?: string;
  variant: ToastVariant;
  duration: number;
  dedupeKey: string;
  phase: "visible" | "exiting";
  onVisibleChange?: (visible: boolean) => void;
}

let entries: ToastEntry[] = [];
let nextId = 0;
const listeners = new Set<(entries: ToastEntry[]) => void>();

function emit() {
  listeners.forEach((listener) => listener(entries));
}

/** Begins a toast's exit transition, then drops it from the stack once the fade-out finishes. */
function dismissToast(id: number) {
  const entry = entries.find((e) => e.id === id);
  if (!entry || entry.phase === "exiting") return;

  entry.onVisibleChange?.(false);
  entries = entries.map((e) => (e.id === id ? { ...e, phase: "exiting" as const } : e));
  emit();

  setTimeout(() => {
    entries = entries.filter((e) => e.id !== id);
    emit();
  }, 300);
}

/** Queues a toast for display in the global stack mounted by <ToastStack /> (see App.tsx). */
export function showToast({
  title,
  description,
  variant = "warning",
  duration,
  dedupeKey,
  onVisibleChange,
}: ShowToastOptions) {
  const key = dedupeKey ?? `${variant}:${title}:${description ?? ""}`;
  if (entries.some((entry) => entry.dedupeKey === key && entry.phase !== "exiting")) {
    return;
  }

  const id = nextId++;
  const entry: ToastEntry = {
    id,
    title,
    variant,
    duration: duration ?? getToastDuration(),
    dedupeKey: key,
    phase: "visible",
    ...(description !== undefined ? { description } : {}),
    ...(onVisibleChange !== undefined ? { onVisibleChange } : {}),
  };
  entries = [...entries, entry];
  emit();
  onVisibleChange?.(true);
}

const VARIANT_STYLES: Record<
  ToastVariant,
  { border: string; icon: ComponentType<{ className?: string }>; iconColor: string; bar: string }
> = {
  success: {
    border: "border-green-200",
    icon: CheckCircle2,
    iconColor: "text-green-500",
    bar: "bg-green-500",
  },
  warning: {
    border: "border-orange-200",
    icon: AlertTriangle,
    iconColor: "text-orange-500",
    bar: "bg-orange-500",
  },
  error: {
    border: "border-red-200",
    icon: AlertCircle,
    iconColor: "text-red-500",
    bar: "bg-red-500",
  },
  info: { border: "border-blue-200", icon: Info, iconColor: "text-blue-500", bar: "bg-blue-500" },
};

function ToastCard({ entry }: { entry: ToastEntry }) {
  const expanded = entry.phase === "visible";
  const [paused, setPaused] = useState(false);

  // Wall-clock bookkeeping so a hover pause/resume can restart the dismiss timer with
  // whatever time was left, while the CSS progress bar (paused via animationPlayState)
  // stays perfectly in sync since it's paused/resumed at the exact same moments.
  const remainingRef = useRef(entry.duration);
  const startRef = useRef(Date.now());
  const timerRef = useRef<ReturnType<typeof setTimeout> | undefined>(undefined);

  useEffect(() => {
    if (entry.phase !== "visible") return;
    startRef.current = Date.now();
    timerRef.current = setTimeout(() => dismissToast(entry.id), remainingRef.current);
    return () => clearTimeout(timerRef.current);
  }, [entry.phase, entry.id]);

  function handleMouseEnter() {
    if (entry.phase !== "visible") return;
    clearTimeout(timerRef.current);
    remainingRef.current = Math.max(0, remainingRef.current - (Date.now() - startRef.current));
    setPaused(true);
  }

  function handleMouseLeave() {
    if (entry.phase !== "visible") return;
    startRef.current = Date.now();
    timerRef.current = setTimeout(() => dismissToast(entry.id), remainingRef.current);
    setPaused(false);
  }

  const { border, icon: Icon, iconColor, bar } = VARIANT_STYLES[entry.variant];

  return (
    <div
      className="inline-grid w-[22rem] justify-self-center overflow-hidden"
      style={{
        gridTemplateRows: expanded ? "1fr" : "0fr",
        marginBottom: expanded ? "0.75rem" : 0,
        opacity: expanded ? 1 : 0,
        transform: expanded ? "translateY(0)" : "translateY(-8px)",
        transition:
          "grid-template-rows 300ms ease, margin-bottom 300ms ease, opacity 300ms ease, transform 300ms ease",
      }}
    >
      <div className="pointer-events-auto min-h-0 overflow-hidden">
        <div
          className={`relative overflow-hidden rounded-xl border bg-white text-sm text-gray-900 shadow-lg ${border}`}
          onMouseEnter={handleMouseEnter}
          onMouseLeave={handleMouseLeave}
        >
          <div className="flex items-start gap-2.5 px-4 py-3">
            <Icon className={`h-4 w-4 shrink-0 mt-0.5 ${iconColor}`} />
            <div className="min-w-0">
              <p className="font-medium leading-snug">{entry.title}</p>
              {entry.description && (
                <p className="mt-0.5 text-xs text-gray-500 leading-snug">{entry.description}</p>
              )}
            </div>
          </div>
          <div className="h-1 w-full bg-gray-100">
            <div
              className={`h-full origin-left ${bar}`}
              style={{
                animation: `sam-toast-progress ${entry.duration}ms linear forwards`,
                animationPlayState: paused ? "paused" : "running",
              }}
            />
          </div>
        </div>
      </div>
    </div>
  );
}

/** Mounted once (see App.tsx) — renders every queued toast as a stack that grows downward and closes the gap when an entry above exits. */
export default function ToastStack() {
  const [items, setItems] = useState<ToastEntry[]>(entries);
  const centerX = useContext(ToastCenterContext);

  useEffect(() => {
    listeners.add(setItems);
    return () => {
      listeners.delete(setItems);
    };
  }, []);

  if (items.length === 0) return null;

  return (
    <>
      <style>{`
        @keyframes sam-toast-progress {
          from { transform: scaleX(1); }
          to { transform: scaleX(0); }
        }
      `}</style>
      <div
        className="pointer-events-none fixed top-20 z-50 grid -translate-x-1/2 justify-items-center"
        style={{ left: centerX != null ? `${centerX}px` : "50%" }}
      >
        {items.map((entry) => (
          <ToastCard key={entry.id} entry={entry} />
        ))}
      </div>
    </>
  );
}
