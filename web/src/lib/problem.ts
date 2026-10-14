export interface ApiProblem {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
  /** Emitted at the root of the problem body by the API (e.g. "validation.failed"). */
  code?: string;
  /** Field-level validation messages, when present. */
  errors?: Record<string, string[]>;
  [key: string]: unknown;
}

export function isApiProblem(value: unknown): value is ApiProblem {
  if (typeof value !== "object" || value === null) {
    return false;
  }
  const candidate = value as Record<string, unknown>;
  return (
    typeof candidate.status === "number" ||
    typeof candidate.title === "string" ||
    typeof candidate.detail === "string" ||
    typeof candidate.code === "string"
  );
}

export function problemMessage(problem: ApiProblem | undefined, fallback: string): string {
  if (!problem) {
    return fallback;
  }
  return problem.detail || problem.title || fallback;
}

/** Returns the first message of each field reported by the API, keyed by the field name. */
export function problemFieldErrors(problem: ApiProblem | undefined): Record<string, string> {
  const result: Record<string, string> = {};
  if (!problem?.errors) {
    return result;
  }
  for (const [field, messages] of Object.entries(problem.errors)) {
    if (messages?.[0]) {
      result[field] = messages[0];
    }
  }
  return result;
}

export class ApiRequestError extends Error {
  readonly status: number;
  readonly problem: ApiProblem | undefined;

  constructor(status: number, problem: unknown) {
    const parsed = isApiProblem(problem) ? problem : undefined;
    super(problemMessage(parsed, `Request failed (status ${status}).`));
    this.name = "ApiRequestError";
    this.status = status;
    this.problem = parsed;
  }

  get fieldErrors(): Record<string, string> {
    return problemFieldErrors(this.problem);
  }
}