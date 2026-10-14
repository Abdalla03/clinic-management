import type { ReactNode } from "react";
import { cn } from "../../lib/cn";

type Tone = "green" | "slate" | "amber" | "red";

export function Badge({ tone = "slate", children }: { tone?: Tone; children: ReactNode }) {
  const classes: Record<Tone, string> = {
    green: "bg-emerald-50 text-emerald-700 ring-emerald-600/20",
    slate: "bg-slate-100 text-slate-700 ring-slate-500/20",
    amber: "bg-amber-50 text-amber-700 ring-amber-600/20",
    red: "bg-red-50 text-red-700 ring-red-600/20",
  };
  return (
    <span
      className={cn(
        "inline-flex items-center gap-1 whitespace-nowrap rounded-full px-2.5 py-0.5 text-xs font-semibold ring-1 ring-inset",
        classes[tone],
      )}
    >
      {children}
    </span>
  );
}