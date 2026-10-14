import type { ReactNode } from "react";

export function EmptyState({ icon, title, hint }: { icon?: ReactNode; title: ReactNode; hint?: ReactNode }) {
  return (
    <div className="flex flex-col items-center gap-2 px-6 py-12 text-center">
      {icon}
      <p className="text-sm font-medium text-slate-700">{title}</p>
      {hint ? <p className="text-sm text-slate-500">{hint}</p> : null}
    </div>
  );
}