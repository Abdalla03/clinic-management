import { useCallback } from "react";
import { useI18n } from "../../lib/i18n/useI18n";
import { ApiRequestError, problemMessage } from "../../lib/problem";

export interface UserMessage {
  title: string;
  message?: string;
}

/**
 * Translates API ProblemDetails / ApiRequestError into a localised toast-friendly message,
 * mapping the well-known error codes the backend emits.
 */
export function useApiErrorMessage() {
  const { t } = useI18n();

  return useCallback(
    (error: unknown): UserMessage => {
      if (!(error instanceof ApiRequestError)) {
        return { title: t("common.error") };
      }
      const code = error.problem?.code ?? "";
      if (code.includes("invalid_credentials")) {
        return { title: t("auth.invalidCredentials") };
      }
      if (code.includes("account_locked")) {
        return { title: t("auth.accountLocked") };
      }
      if (code.includes("account_disabled")) {
        return { title: t("auth.accountDisabled") };
      }
      if (code.includes("mfa_mandatory")) {
        return { title: t("auth.mfaRequired"), message: error.message };
      }
      return {
        title: problemMessage(error.problem, t("common.error")),
        message: error.message !== error.problem?.title ? error.message : undefined,
      };
    },
    [t],
  );
}