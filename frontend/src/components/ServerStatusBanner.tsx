import { useQueryClient } from "@tanstack/react-query";
import { useEffect, useState } from "react";
import { useServerReachable } from "../api/admissionApiClient";
import { useLanguage } from "../i18n/LanguageProvider";
import { Button } from "./Button";

// Shown on every page while the server cannot be reached. The last data stays on
// screen underneath, and the polling keeps retrying by itself.
export function ServerStatusBanner() {
  const { t } = useLanguage();
  const queryClient = useQueryClient();
  const reachable = useServerReachable();
  const [browserOnline, setBrowserOnline] = useState(() => navigator.onLine);
  const [retrying, setRetrying] = useState(false);

  useEffect(() => {
    const update = () => setBrowserOnline(navigator.onLine);
    window.addEventListener("online", update);
    window.addEventListener("offline", update);
    return () => {
      window.removeEventListener("online", update);
      window.removeEventListener("offline", update);
    };
  }, []);

  if (reachable && browserOnline) return null;

  async function retryNow() {
    setRetrying(true);
    try {
      await queryClient.refetchQueries({ type: "active" });
    } finally {
      setRetrying(false);
    }
  }

  return (
    <div className="server-banner" role="alert">
      <div className="page-width server-banner__inner">
        <p>
          {t("server.unreachable")} {t("server.oldData")}
        </p>
        <Button variant="secondary" pending={retrying} onClick={retryNow}>
          {t("server.retry")}
        </Button>
      </div>
    </div>
  );
}
