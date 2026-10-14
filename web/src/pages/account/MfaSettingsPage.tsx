import { useMutation } from "@tanstack/react-query";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { QRCodeSVG } from "qrcode.react";
import { z } from "zod";
import { zodResolver } from "@hookform/resolvers/zod";
import { Button } from "../../components/ui/Button";
import { Card, CardBody, CardHeader } from "../../components/ui/Card";
import { Field, Input } from "../../components/ui/Field";
import { Badge } from "../../components/ui/Badge";
import { useAuth } from "../../features/auth/useAuth";
import { useApiErrorMessage } from "../../features/errors/useApiErrorMessage";
import { beginMfaEnrollment, confirmMfaEnrollment, disableMfa, type MfaEnrollment } from "../../lib/api/auth";
import { useI18n } from "../../lib/i18n/useI18n";
import { useToast } from "../../lib/toast/useToast";

const codeSchema = z.object({ code: z.string().trim().min(6).max(6) });
const passwordSchema = z.object({ password: z.string().min(1) });

export function MfaSettingsPage() {
  const { t } = useI18n();
  const toast = useToast();
  const apiMessage = useApiErrorMessage();
  const { user, refreshUser } = useAuth();

  const [enrollment, setEnrollment] = useState<MfaEnrollment | null>(null);
  const [copied, setCopied] = useState(false);

  const codeForm = useForm<z.infer<typeof codeSchema>>({ resolver: zodResolver(codeSchema), defaultValues: { code: "" } });
  const disableForm = useForm<z.infer<typeof passwordSchema>>({
    resolver: zodResolver(passwordSchema),
    defaultValues: { password: "" },
  });

  const enrollMutation = useMutation({
    mutationFn: beginMfaEnrollment,
    onSuccess: (result) => setEnrollment(result),
    onError: (error) => {
      const message = apiMessage(error);
      toast.error({ title: message.title, message: message.message });
    },
  });

  const confirmMutation = useMutation({
    mutationFn: ({ code }: z.infer<typeof codeSchema>) => confirmMfaEnrollment(code),
    onSuccess: () => {
      setEnrollment(null);
      codeForm.reset();
      refreshUser();
      toast.success({ title: t("mfa.enableSuccess") });
    },
    onError: (error) => {
      const message = apiMessage(error);
      codeForm.setError("code", { message: message.title });
    },
  });

  const disableMutation = useMutation({
    mutationFn: ({ password }: z.infer<typeof passwordSchema>) => disableMfa(password),
    onSuccess: () => {
      disableForm.reset();
      refreshUser();
      toast.success({ title: t("mfa.disableSuccess") });
    },
    onError: (error) => {
      const message = apiMessage(error);
      disableForm.setError("password", { message: message.title });
    },
  });

  const mfaEnabled = user?.mfaEnabled ?? false;

  const copySecret = async () => {
    if (!enrollment?.base32Secret) return;
    try {
      await navigator.clipboard.writeText(enrollment.base32Secret);
      setCopied(true);
      toast.info({ title: t("common.copied") });
      window.setTimeout(() => setCopied(false), 2000);
    } catch {
      toast.error({ title: t("common.error") });
    }
  };

  return (
    <div className="flex flex-col gap-6">
      <div className="flex flex-col gap-1">
        <h1 className="text-xl font-bold tracking-tight text-slate-900">{t("nav.security")}</h1>
        <p className="text-sm text-slate-500">{t("nav.account")}</p>
      </div>

      <Card>
        <CardHeader
          title={t("mfa.enable")}
          actions={
            mfaEnabled ? <Badge tone="green">{t("mfa.enabled")}</Badge> : <Badge tone="slate">{t("mfa.disabled")}</Badge>
          }
        />
        <CardBody>
          {mfaEnabled ? (
            <div className="flex flex-col gap-5">
              <p className="text-sm text-slate-600">{t("mfa.enabledMessage")}</p>
              <form
                onSubmit={disableForm.handleSubmit((values) => disableMutation.mutate(values))}
                className="flex max-w-sm flex-col gap-4"
              >
                <Field label={t("mfa.currentPassword")} htmlFor="disable-password" required>
                  <Input
                    id="disable-password"
                    type="password"
                    autoComplete="current-password"
                    invalid={Boolean(disableForm.formState.errors.password)}
                    {...disableForm.register("password")}
                  />
                </Field>
                {disableForm.formState.errors.password ? (
                  <p role="alert" className="text-xs font-medium text-red-600">
                    {disableForm.formState.errors.password.message}
                  </p>
                ) : null}
                <Button variant="danger" type="submit" loading={disableMutation.isPending}>
                  {t("mfa.disable")}
                </Button>
              </form>
            </div>
          ) : enrollment ? (
            <div className="flex flex-col gap-6">
              <div className="flex flex-col items-center gap-3">
                <p className="text-sm text-slate-600">{t("mfa.scanQr")}</p>
                <div className="rounded-xl border border-slate-200 bg-white p-4">
                  <QRCodeSVG value={enrollment.provisioningUri ?? ""} size={208} />
                </div>
                <p className="text-sm text-slate-600">{t("mfa.orEnterSecret")}</p>
                <div className="flex items-center gap-2 rounded-lg border border-slate-200 bg-slate-50 px-3 py-2">
                  <code className="text-sm font-medium tracking-wider text-slate-700">{enrollment.base32Secret}</code>
                  <Button variant="ghost" size="sm" onClick={copySecret}>
                    {copied ? "✓" : t("common.copy")}
                  </Button>
                </div>
              </div>

              <form
                onSubmit={codeForm.handleSubmit((values) => confirmMutation.mutate(values))}
                className="mx-auto flex w-full max-w-sm flex-col gap-4"
              >
                <p className="text-sm text-slate-600">{t("mfa.confirmCode")}</p>
                <Field label={t("auth.code")} htmlFor="mfa-confirm-code" required>
                  <Input
                    id="mfa-confirm-code"
                    inputMode="numeric"
                    autoComplete="one-time-code"
                    maxLength={6}
                    className="text-center text-lg tracking-[0.4em]"
                    invalid={Boolean(codeForm.formState.errors.code)}
                    {...codeForm.register("code")}
                  />
                </Field>
                {codeForm.formState.errors.code ? (
                  <p role="alert" className="text-xs font-medium text-red-600">
                    {codeForm.formState.errors.code.message}
                  </p>
                ) : null}
                <Button type="submit" loading={confirmMutation.isPending} className="w-full">
                  {confirmMutation.isPending ? t("mfa.confirming") : t("mfa.confirm")}
                </Button>
              </form>
            </div>
          ) : (
            <div className="flex flex-col gap-4">
              <p className="text-sm text-slate-600">{t("mfa.disabledMessage")}</p>
              <Button onClick={() => enrollMutation.mutate()} loading={enrollMutation.isPending} className="w-fit">
                {t("mfa.enable")}
              </Button>
            </div>
          )}
        </CardBody>
      </Card>
    </div>
  );
}

export default MfaSettingsPage;