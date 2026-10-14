import { useContext } from "react";
import { ToastContext } from "./ToastProvider";
import type { ToastApi } from "./toast-store";

export function useToast(): ToastApi {
  const context = useContext(ToastContext);
  if (!context) {
    throw new Error("useToast must be used inside a ToastProvider.");
  }
  return context;
}