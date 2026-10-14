/**
 * White-label branding. The clinic name and primary colour are read from the build/runtime
 * environment (`web/.env` → `VITE_CLINIC_NAME`, `VITE_PRIMARY_COLOR`) so the same image can be
 * re-branded per deployment without code changes. Values are applied at startup: the document
 * title is set and the `--color-primary-*` design tokens are overridden on `:root`.
 */

const DEFAULT_CLINIC_NAME = "Clinic Management";
const DEFAULT_PRIMARY_COLOR = "#217d6b";

function readEnv(key: string): string | undefined {
  const value = (import.meta.env as unknown as Record<string, string | undefined>)[key];
  return typeof value === "string" && value.trim().length > 0 ? value.trim() : undefined;
}

function normalizeHex(value: string): string | undefined {
  const hex = value.trim().replace(/^#/, "");
  if (/^[0-9a-fA-F]{3}$/.test(hex)) {
    return `#${hex
      .split("")
      .map((char) => char + char)
      .join("")}`.toLowerCase();
  }
  if (/^[0-9a-fA-F]{6}$/.test(hex)) {
    return `#${hex.toLowerCase()}`;
  }
  return undefined;
}

export const clinicName: string = readEnv("VITE_CLINIC_NAME") ?? DEFAULT_CLINIC_NAME;
export const primaryColor: string = normalizeHex(readEnv("VITE_PRIMARY_COLOR") ?? "") ?? DEFAULT_PRIMARY_COLOR;

const LIGHT_SHADES: Record<string, number> = {
  "50": 8,
  "100": 16,
  "200": 28,
  "300": 42,
  "400": 58,
};

const DARK_SHADES: Record<string, number> = {
  "600": 86,
  "700": 72,
  "800": 58,
  "900": 44,
  "950": 26,
};

function buildPaletteCss(hex: string): string {
  const lines: string[] = [`  --color-primary-500: ${hex};`];
  for (const [shade, weight] of Object.entries(LIGHT_SHADES)) {
    lines.push(`  --color-primary-${shade}: color-mix(in srgb, ${hex} ${weight}%, white);`);
  }
  for (const [shade, weight] of Object.entries(DARK_SHADES)) {
    lines.push(`  --color-primary-${shade}: color-mix(in srgb, ${hex} ${weight}%, black);`);
  }
  return `:root {\n${lines.join("\n")}\n}`;
}

export function applyBranding(): void {
  if (typeof document === "undefined") {
    return;
  }

  document.title = clinicName;

  const styleId = "clinic-branding";
  let style = document.getElementById(styleId) as HTMLStyleElement | null;
  if (!style) {
    style = document.createElement("style");
    style.id = styleId;
    document.head.appendChild(style);
  }
  style.textContent = buildPaletteCss(primaryColor);
}
