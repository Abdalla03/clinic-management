import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation } from "@tanstack/react-query";
import { useForm } from "react-hook-form";
import { Navigate, useLocation, useNavigate } from "react-router-dom";
import { z } from "zod";
import { LanguageToggle } from "../components/LanguageToggle";
import { Button } from "../components/ui/Button";
import { Field, Input } from "../components/ui/Field";
import { verifyMfa } from "../lib/api/auth";
import { useI18n } from "../lib/i18n/useI18n";

const codeSchema = z.object({
  code: z.string().trim().min(6, "required").max(6, "required"),
});

export function MfaVerifyPage() {
  const { t } = useI18n();
  const navigate = useNavigate();
  const location = useLocation();

  const state = (location.state ?? {}) as { mfaPendingToken?: string; from?: string };
  const mfaPendingToken =
    state.mfaPendingToken ?? new URLSearchParams(location.search).get("mfaPendingToken");

  const form = useForm<z.infer<typeof codeSchema>>({
    resolver: zodResolver(codeSchema),
    defaultValues: { code: "" },
  });

  const mutation = useMutation({
    mutationFn: ({ code }: z.infer<typeof codeSchema>) => verifyMfa(mfaPendingToken ?? "", code),
    onSuccess: () => {
      navigate(state.from ?? "/", { replace: true });
    },
    onError: () => {
      form.setError("code", { message: t("auth.invalidMfaCode") });
    },
  });

  if (!mfaPendingToken) {
    return <Navigate to="/login" replace />;
  }

  return (
    <div className="flex min-h-screen flex-col bg-gradient-to-br from-primary-50 via-slate-50 to-slate-100">
      <div className="flex justify-between p-4">
        <button
          type="button"
          onClick={() => navigate("/login")}
          className="text-sm font-medium text-slate-500 hover:text-slate-800"
        >
          ← {t("common.back")}
        </button>
        <LanguageToggle />
      </div>
      <div className="flex flex-1 items-center justify-center px-4 pb-12">
        <div className="w-full max-w-md rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
          <div className="mb-6 text-center">
            <h1 className="text-xl font-bold text-slate-900">{t("auth.mfaRequired")}</h1>
            <p className="mt-1 text-sm text-slate-500">{t("auth.mfaSubtitle")}</p>
          </div>

          <form
            onSubmit={form.handleSubmit((values) => mutation.mutate(values))}
            className="flex flex-col gap-4"
          >
            <Field label={t("auth.code")} htmlFor="mfaCode" required>
              <Input
                id="mfaCode"
                inputMode="numeric"
                autoComplete="one-time-code"
                maxLength={6}
                autoFocus
                className="text-center text-lg tracking-[0.4em]"
                invalid={Boolean(form.formState.errors.code)}
                {...form.register("code")}
              />
            </Field>
            {form.formState.errors.code ? (
              <p role="alert" className="text-xs font-medium text-red-600">
                {form.formState.errors.code.message}
              </p>
            ) : null}

            <Button type="submit" loading={mutation.isPending} className="w-full">
              {mutation.isPending ? t("auth.verifying") : t("auth.verify")}
            </Button>
          </form>
        </div>
      </div>
    </div>
  );
}

export default MfaVerifyPage;