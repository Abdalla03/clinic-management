import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation } from "@tanstack/react-query";
import { useForm } from "react-hook-form";
import { Navigate, useLocation, useNavigate } from "react-router-dom";
import { z } from "zod";
import { AmbientBackground } from "../components/AmbientBackground";
import { LanguageToggle } from "../components/LanguageToggle";
import { ThemeToggle } from "../components/ThemeToggle";
import { Button } from "../components/ui/Button";
import { Field, Input } from "../components/ui/Field";
import { login } from "../lib/api/auth";
import { clinicName } from "../lib/branding";
import { useI18n } from "../lib/i18n/useI18n";
import type { TranslationKey } from "../lib/i18n/translations";
import { useToast } from "../lib/toast/useToast";
import { useApiErrorMessage } from "../features/errors/useApiErrorMessage";
import { useAuth } from "../features/auth/useAuth";

const demoAccounts = [
  { email: "doctor@clinic.local", password: "Doctor!Clinic2026", roleKey: "role.doctor" },
  { email: "secretary@clinic.local", password: "Secretary!Clinic2026", roleKey: "role.secretary" },
  { email: "patient@clinic.local", password: "Patient!Clinic2026", roleKey: "role.patient" },
] as const;

const featuredKeys: TranslationKey[] = ["patient.title", "appointment.title", "invoice.title"];

function ClinicMark({ className }: { className?: string }) {
  return (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" className={className} aria-hidden="true">
      <path d="M19 14c1.49-1.46 3-3.21 3-5.5A5.5 5.5 0 0 0 16.5 3c-1.76 0-3 .5-4.5 2-1.5-1.5-2.74-2-4.5-2A5.5 5.5 0 0 0 2 8.5c0 2.3 1.5 4.05 3 5.5l7 7Z" />
    </svg>
  );
}

export function LoginPage() {
  const { t } = useI18n();
  const toast = useToast();
  const apiMessage = useApiErrorMessage();
  const navigate = useNavigate();
  const location = useLocation();
  const { isAuthenticated } = useAuth();

  const state = (location.state ?? {}) as { from?: string; reason?: string };
  const from = state.from ?? "/";

  const schema = z.object({
    emailOrUserName: z.string().min(1, t("common.required")),
    password: z.string().min(1, t("common.required")),
  });

  const form = useForm<z.infer<typeof schema>>({
    resolver: zodResolver(schema),
    defaultValues: { emailOrUserName: "", password: "" },
  });

  const mutation = useMutation({
    mutationFn: ({ emailOrUserName, password }: z.infer<typeof schema>) =>
      login(emailOrUserName, password),
    onSuccess: (result) => {
      if (result.mfaRequired && result.mfaPendingToken) {
        navigate("/mfa-verify", {
          state: { mfaPendingToken: result.mfaPendingToken, from },
          replace: true,
        });
        return;
      }
      navigate(from, { replace: true });
    },
    onError: (error) => {
      const message = apiMessage(error);
      form.setError("root", { message: message.title });
      toast.error({ title: message.title, message: message.message });
    },
  });

  if (isAuthenticated) {
    return <Navigate to="/" replace />;
  }

  return (
    <div className="relative flex min-h-screen flex-col lg:flex-row">
      <AmbientBackground variant="auth" />

      <section className="relative hidden overflow-hidden bg-slate-900 p-12 text-white lg:flex lg:w-1/2 lg:flex-col lg:justify-between">
        <div className="absolute -top-24 -left-24 size-80 rounded-full bg-primary-500/40 blur-3xl motion-safe:animate-drift" />
        <div className="absolute right-0 bottom-0 size-96 rounded-full bg-sky-500/20 blur-3xl motion-safe:animate-drift-slow" />

        <div className="relative flex items-center gap-3">
          <span className="flex size-10 items-center justify-center rounded-xl bg-primary-600 text-white shadow-lg">
            <ClinicMark className="size-6" />
          </span>
          <span className="text-lg font-bold">{clinicName}</span>
        </div>

        <div className="relative motion-safe:animate-fade-up">
          <h2 className="max-w-md text-3xl font-bold leading-tight">{t("auth.loginTitle")}</h2>
          <p className="mt-3 max-w-md text-slate-300">{t("auth.loginSubtitle")}</p>
          <ul className="mt-8 flex flex-col gap-3 text-sm text-slate-200">
            {featuredKeys.map((key) => (
              <li key={key} className="flex items-center gap-3">
                <span className="flex size-6 items-center justify-center rounded-full bg-primary-500/25 text-primary-200">
                  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.4" className="size-3.5" aria-hidden="true">
                    <path d="M20 6 9 17l-5-5" />
                  </svg>
                </span>
                {t(key)}
              </li>
            ))}
          </ul>
        </div>

        <p className="relative text-xs text-slate-400">
          © {new Date().getFullYear()} {clinicName}
        </p>
      </section>

      <section className="relative flex flex-1 flex-col">
        <div className="flex justify-end gap-2 p-4">
          <LanguageToggle />
          <ThemeToggle />
        </div>

        <div className="flex flex-1 items-center justify-center px-4 pb-12">
          <div className="w-full max-w-md motion-safe:animate-fade-up">
            <div className="mb-6 flex flex-col items-center gap-3 text-center lg:hidden">
              <span className="flex size-12 items-center justify-center rounded-2xl bg-primary-600 text-white shadow-lg">
                <ClinicMark className="size-7" />
              </span>
              <div>
                <h1 className="text-2xl font-bold tracking-tight text-slate-900 dark:text-white">
                  {t("auth.loginTitle")}
                </h1>
                <p className="mt-1 text-sm text-slate-500">{clinicName}</p>
              </div>
            </div>

            <div className="glass rounded-2xl border border-slate-200 p-6 shadow-xl shadow-slate-900/5 dark:border-slate-800">
              {state.reason === "session-expired" ? (
                <p className="mb-4 rounded-lg bg-amber-50 px-3 py-2 text-sm text-amber-800">
                  {t("auth.sessionExpired")}
                </p>
              ) : null}

              {form.formState.errors.root ? (
                <p role="alert" className="mb-4 rounded-lg bg-red-50 px-3 py-2 text-sm text-red-700">
                  {form.formState.errors.root.message}
                </p>
              ) : null}

              <form
                onSubmit={form.handleSubmit((values) => mutation.mutate(values))}
                className="flex flex-col gap-4"
              >
                <Field label={t("auth.emailOrUserName")} htmlFor="emailOrUserName" required>
                  <Input
                    id="emailOrUserName"
                    autoComplete="username"
                    autoFocus
                    invalid={Boolean(form.formState.errors.emailOrUserName)}
                    {...form.register("emailOrUserName")}
                  />
                </Field>
                {form.formState.errors.emailOrUserName ? (
                  <p className="-mt-3 text-xs font-medium text-red-600">
                    {form.formState.errors.emailOrUserName.message}
                  </p>
                ) : null}

                <Field label={t("auth.password")} htmlFor="password" required>
                  <Input
                    id="password"
                    type="password"
                    autoComplete="current-password"
                    invalid={Boolean(form.formState.errors.password)}
                    {...form.register("password")}
                  />
                </Field>
                {form.formState.errors.password ? (
                  <p className="-mt-3 text-xs font-medium text-red-600">
                    {form.formState.errors.password.message}
                  </p>
                ) : null}

                <Button type="submit" loading={mutation.isPending} className="mt-1 w-full">
                  {mutation.isPending ? t("auth.signingIn") : t("auth.signIn")}
                </Button>
              </form>

              <details className="mt-5 rounded-lg bg-slate-50 px-3 py-2 text-xs text-slate-500 dark:bg-slate-800/60">
                <summary className="cursor-pointer font-medium text-slate-600 dark:text-slate-300">
                  Demo sign-ins
                </summary>
                <ul className="mt-2 flex flex-col gap-1">
                  {demoAccounts.map((account) => (
                    <li key={account.email} className="flex items-center justify-between gap-2">
                      <span className="font-medium text-slate-700 dark:text-slate-200">{t(account.roleKey)}</span>
                      <code className="text-slate-600 dark:text-slate-300">{account.email}</code>
                    </li>
                  ))}
                </ul>
                <p className="mt-2">Passwords are in the project README.</p>
              </details>
            </div>
          </div>
        </div>
      </section>
    </div>
  );
}

export default LoginPage;
