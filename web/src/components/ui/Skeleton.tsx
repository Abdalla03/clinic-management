import { cn } from "../../lib/cn";

export function Skeleton({ className, ...rest }: React.HTMLAttributes<HTMLDivElement>) {
  return (
    <div
      aria-hidden="true"
      className={cn("animate-pulse rounded-md bg-slate-200/80 dark:bg-slate-700/60", className)}
      {...rest}
    />
  );
}

export default Skeleton;
