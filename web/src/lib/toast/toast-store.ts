export type ToastTone = "success" | "error" | "info";

export interface ToastItem {
  id: number;
  tone: ToastTone;
  title: string;
  message?: string;
}

export interface ToastInput {
  title: string;
  message?: string;
}

export interface ToastApi {
  success: (input: ToastInput) => void;
  error: (input: ToastInput) => void;
  info: (input: ToastInput) => void;
}