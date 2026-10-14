import { createContext, useCallback, useMemo, useState, type ReactNode } from "react";
import type { ToastApi, ToastInput, ToastItem, ToastTone } from "./toast-store";

export const ToastContext = createContext<ToastApi | null>(null);

let nextId = 1;

function toneStyles(tone: ToastTone): {
  ring: string;
  icon: string;
} {
  switch (tone) {
    case "success":
      return { ring: "border-emerald-200 ring-emerald-100", icon: "bg-emerald-500" };
    case "error":
      return { ring: "border-red-200 ring-red-100", icon: "bg-red-500" };
    case "info":
      return { ring: "border-sky-200 ring-sky-100", icon: "bg-sky-500" };
  }
}

export function ToastProvider({ children }: { children: ReactNode }) {
  const [toasts, setToasts] = useState<ToastItem[]>([]);

  const dismiss = useCallback((id: number) => {
    setToasts((current) => current.filter((item) => item.id !== id));
  }, []);

  const push = useCallback((tone: ToastTone, { title, message }: ToastInput) => {
    const id = nextId++;
    setToasts((current) => [...current, { id, tone, title, message }]);
    window.setTimeout(() => dismiss(id), tone === "error" ? 6000 : 4000);
  }, [dismiss]);

  const api = useMemo<ToastApi>(
    () => ({
      success: (input) => push("success", input),
      error: (input) => push("error", input),
      info: (input) => push("info", input),
    }),
    [push],
  );

  return (
    <ToastContext.Provider value={api}>
      {children}
      <div
        aria-live="polite"
        className="pointer-events-none fixed inset-x-0 bottom-4 z-50 flex flex-col items-center gap-2 px-4 sm:items-end sm:pe-6"
      >
        {toasts.map((toast) => {
          const styles = toneStyles(toast.tone);
          return (
            <div
              key={toast.id}
              role="status"
              className={`pointer-events-auto flex w-full max-w-sm items-start gap-3 rounded-xl border bg-white p-4 shadow-lg ring-4 ${styles.ring}`}
            >
              <span
                aria-hidden="true"
                className={`mt-0.5 flex size-2.5 shrink-0 rounded-full ${styles.icon}`}
              />
              <div className="min-w-0 flex-1">
                <p className="text-sm font-semibold text-slate-900">{toast.title}</p>
                {toast.message ? (
                  <p className="mt-1 text-sm leading-5 text-slate-600">{toast.message}</p>
                ) : null}
              </div>
              <button
                type="button"
                onClick={() => dismiss(toast.id)}
                className="shrink-0 rounded-md p-1 text-slate-400 hover:bg-slate-100 hover:text-slate-600"
                aria-label="Dismiss"
              >
                <svg viewBox="0 0 20 20" fill="currentColor" className="size-4" aria-hidden="true">
                  <path d="M6.28 5.22a.75.75 0 0 0-1.06 1.06L8.94 10l-3.72 3.72a.75.75 0 1 0 1.06 1.06L10 11.06l3.72 3.72a.75.75 0 1 0 1.06-1.06L11.06 10l3.72-3.72a.75.75 0 0 0-1.06-1.06L10 8.94 6.28 5.22Z" />
                </svg>
              </button>
            </div>
          );
        })}
      </div>
    </ToastContext.Provider>
  );
}