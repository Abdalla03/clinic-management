/**
 * Decorative animated "mesh" background: a few soft colour blobs that drift behind the content
 * plus a faint medical-cross motif. Purely presentational, so it is hidden from assistive tech and
 * never captures pointer events.
 */
export function AmbientBackground({ variant = "app" }: { variant?: "app" | "auth" }) {
  const intensity = variant === "auth" ? "opacity-70" : "opacity-50";

  return (
    <div aria-hidden="true" className={`pointer-events-none fixed inset-0 -z-10 overflow-hidden ${intensity}`}>
      <div className="absolute -top-32 -left-24 size-[32rem] rounded-full bg-primary-400/40 blur-3xl motion-safe:animate-drift" />
      <div className="absolute top-1/3 -right-32 size-[38rem] rounded-full bg-sky-400/30 blur-3xl motion-safe:animate-drift-slow" />
      <div className="absolute -bottom-40 left-1/4 size-[34rem] rounded-full bg-emerald-300/30 blur-3xl motion-safe:animate-drift" />

      <svg className="absolute inset-0 size-full text-primary-600/10" xmlns="http://www.w3.org/2000/svg">
        <defs>
          <pattern id="clinic-crosses" width="72" height="72" patternUnits="userSpaceOnUse">
            <path
              d="M34 22h4v10h10v4H38v10h-4V36H24v-4h10z"
              fill="currentColor"
            />
          </pattern>
        </defs>
        <rect width="100%" height="100%" fill="url(#clinic-crosses)" />
      </svg>
    </div>
  );
}

export default AmbientBackground;
