import { useEffect } from "react";
import { useNavigate } from "react-router-dom";
import { useI18n } from "../../lib/i18n/useI18n";
import { useToast } from "../../lib/toast/useToast";

/**
 * Rendered inside the router so it can redirect to the login page when the API
 * reports an expired or unrefreshable session.
 */
export function AuthSessionListener() {
  const navigate = useNavigate();
  const { t } = useI18n();
  const toast = useToast();

  useEffect(() => {
    const onSessionExpired = () => {
      toast.error({ title: t("auth.sessionExpired") });
      navigate("/login", { replace: true });
    };
    window.addEventListener("auth:session-expired", onSessionExpired);
    return () => window.removeEventListener("auth:session-expired", onSessionExpired);
  }, [navigate, t, toast]);

  return null;
}