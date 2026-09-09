"use client";

import { useEffect, useRef, useState } from "react";
import { Play, Pause, RotateCcw, ChevronLeft, ChevronRight } from "lucide-react";
import { cn } from "@/lib/utils";
import { Button } from "@/components/ui/button";
import { DraggablePanel } from "@/components/ui/draggable-panel";
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from "@/components/ui/tooltip";
import { useT } from "@/lib/i18n";

const DURATION_OPTIONS = [
  { key: "75", minutes: 75 },
  { key: "60", minutes: 60 },
  { key: "45", minutes: 45 },
] as const;

type DurationKey = (typeof DURATION_OPTIONS)[number]["key"];

function formatTime(totalSeconds: number): string {
  const clamped = Math.max(0, totalSeconds);
  const minutes = Math.floor(clamped / 60);
  const seconds = clamped % 60;
  return `${String(minutes).padStart(2, "0")}:${String(seconds).padStart(2, "0")}`;
}

export function CountdownTimer() {
  const t = useT();
  const [selectedDuration, setSelectedDuration] = useState<DurationKey>("75");
  const [remainingSeconds, setRemainingSeconds] = useState<number>(75 * 60);
  const [isRunning, setIsRunning] = useState(false);
  const intervalRef = useRef<ReturnType<typeof setInterval> | null>(null);

  const isFinished = remainingSeconds === 0 && !isRunning;

  const isRunningRef = useRef(isRunning);
  useEffect(() => {
    isRunningRef.current = isRunning;
  }, [isRunning]);

  useEffect(() => {
    if (isRunning) {
      intervalRef.current = setInterval(() => {
        setRemainingSeconds((prev) => {
          if (prev <= 1) {
            if (isRunningRef.current) {
              setIsRunning(false);
            }
            if (intervalRef.current) {
              clearInterval(intervalRef.current);
              intervalRef.current = null;
            }
            return 0;
          }
          return prev - 1;
        });
      }, 1000);
    }
    return () => {
      if (intervalRef.current) {
        clearInterval(intervalRef.current);
        intervalRef.current = null;
      }
    };
  }, [isRunning]);

  const handleDurationChange = (key: DurationKey) => {
    setSelectedDuration(key);
    const option = DURATION_OPTIONS.find((d) => d.key === key);
    if (option) {
      setRemainingSeconds(option.minutes * 60);
    }
    setIsRunning(false);
    if (intervalRef.current) {
      clearInterval(intervalRef.current);
      intervalRef.current = null;
    }
  };

  const handleStart = () => {
    if (remainingSeconds > 0) {
      setIsRunning(true);
    }
  };

  const handlePause = () => {
    setIsRunning(false);
  };

  const handleReset = () => {
    setIsRunning(false);
    const option = DURATION_OPTIONS.find((d) => d.key === selectedDuration);
    setRemainingSeconds((option?.minutes ?? 75) * 60);
  };

  const currentIndex = (() => {
    const i = DURATION_OPTIONS.findIndex((d) => d.key === selectedDuration);
    return i === -1 ? 0 : i;
  })();
  const handlePrevDuration = () => {
    if (isRunning) return;
    const next = (currentIndex - 1 + DURATION_OPTIONS.length) % DURATION_OPTIONS.length;
    const opt = DURATION_OPTIONS[next];
    if (opt) handleDurationChange(opt.key);
  };
  const handleNextDuration = () => {
    if (isRunning) return;
    const next = (currentIndex + 1) % DURATION_OPTIONS.length;
    const opt = DURATION_OPTIONS[next];
    if (opt) handleDurationChange(opt.key);
  };

  const prevOption =
    DURATION_OPTIONS[(currentIndex - 1 + DURATION_OPTIONS.length) % DURATION_OPTIONS.length] ??
    DURATION_OPTIONS[0];
  const currentOption = DURATION_OPTIONS[currentIndex] ?? DURATION_OPTIONS[0];
  const nextOption =
    DURATION_OPTIONS[(currentIndex + 1) % DURATION_OPTIONS.length] ?? DURATION_OPTIONS[0];

  return (
    <TooltipProvider delayDuration={150}>
      <DraggablePanel
        className="fixed z-50"
        defaultPosition={{ x: 16, y: 16 }}
        defaultAnchor="top-right"
        clampToViewport
        handle="children"
      >
        <div
          data-draggable-handle
          role={isFinished ? "button" : undefined}
          tabIndex={isFinished ? 0 : undefined}
          onClick={(e) => {
            if (!isFinished) return;
            e.stopPropagation();
            handleReset();
          }}
          onKeyDown={(e) => {
            if (!isFinished) return;
            if (e.key === "Enter" || e.key === " ") {
              e.preventDefault();
              handleReset();
            }
          }}
          className={cn(
            "group flex items-center gap-1.5 rounded-xl",
            "bg-card/95 backdrop-blur-md shadow-paper px-2 py-1.5",
            "cursor-grab active:cursor-grabbing",
            isFinished && "animate-shake animate-pulse-ring cursor-pointer outline-none",
          )}
        >
          <div
            className={cn(
              "flex items-center justify-center rounded-lg px-3 py-1",
              "bg-muted/50",
              isFinished
                ? "bg-destructive/10 text-destructive"
                : remainingSeconds <= 5 * 60 && isRunning
                  ? "bg-warning/10 text-warning"
                  : "text-card-foreground",
            )}
          >
            <div className={cn(isFinished && "animate-pulse")}>
              <span className="text-numeric text-lg font-semibold tracking-tight">
                {formatTime(remainingSeconds)}
              </span>
            </div>
          </div>

          {!isFinished ? (
            <div className="flex items-center gap-0.5">
              <Tooltip>
                <TooltipTrigger asChild>
                  <Button
                    type="button"
                    size="icon"
                    variant="ghost"
                    className="size-6"
                    onClick={handlePrevDuration}
                    disabled={isRunning}
                  >
                    <ChevronLeft className="size-3.5" />
                  </Button>
                </TooltipTrigger>
                <TooltipContent side="bottom">
                  <p>{t(`countdown.duration.${prevOption.key}` as const)}</p>
                </TooltipContent>
              </Tooltip>

              <div
                className={cn(
                  "flex items-center justify-center px-1 h-6 min-w-[48px] text-[11px] font-medium",
                  remainingSeconds <= 5 * 60 && isRunning ? "text-warning" : "text-card-foreground",
                )}
              >
                <span className="text-numeric leading-none">
                  {t(`countdown.duration.${currentOption.key}` as const)}
                </span>
              </div>

              <Tooltip>
                <TooltipTrigger asChild>
                  <Button
                    type="button"
                    size="icon"
                    variant="ghost"
                    className="size-6"
                    onClick={handleNextDuration}
                    disabled={isRunning}
                  >
                    <ChevronRight className="size-3.5" />
                  </Button>
                </TooltipTrigger>
                <TooltipContent side="bottom">
                  <p>{t(`countdown.duration.${nextOption.key}` as const)}</p>
                </TooltipContent>
              </Tooltip>
            </div>
          ) : (
            <div className="flex items-center">
              <div className="flex items-center justify-center px-1 h-6 text-[11px] font-semibold text-destructive min-w-[72px]">
                <span className="leading-none">
                  <span className="hidden group-hover:inline-flex items-center gap-1">
                    <RotateCcw className="size-3.5" />
                    {t("countdown.reset")}
                  </span>
                  <span className="group-hover:hidden">{t("countdown.finished")}</span>
                </span>
              </div>
            </div>
          )}

          {!isFinished ? (
            <div className="flex items-center gap-0.5 pl-0.5">
              <Tooltip>
                <TooltipTrigger asChild>
                  <Button
                    type="button"
                    size="icon"
                    variant="ghost"
                    className={cn(
                      "size-6",
                      isFinished && "hover:bg-destructive/15 text-destructive",
                    )}
                    onClick={!isRunning ? handleStart : handlePause}
                    disabled={!isRunning && remainingSeconds === 0}
                  >
                    {!isRunning ? <Play className="size-3.5" /> : <Pause className="size-3.5" />}
                  </Button>
                </TooltipTrigger>
                <TooltipContent side="bottom">
                  <p>{!isRunning ? t("countdown.start") : t("countdown.pause")}</p>
                </TooltipContent>
              </Tooltip>

              <Tooltip>
                <TooltipTrigger asChild>
                  <Button
                    type="button"
                    size="icon"
                    variant="ghost"
                    className={cn("size-6", isFinished && "hover:bg-destructive/15")}
                    onClick={handleReset}
                  >
                    <RotateCcw className="size-3.5" />
                  </Button>
                </TooltipTrigger>
                <TooltipContent side="bottom">
                  <p>{t("countdown.reset")}</p>
                </TooltipContent>
              </Tooltip>
            </div>
          ) : null}
        </div>
      </DraggablePanel>
    </TooltipProvider>
  );
}
