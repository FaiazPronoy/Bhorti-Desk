import type { UseQueryResult } from "@tanstack/react-query";
import type { ReactNode } from "react";
import { Link } from "react-router";
import { errorMessageKey, isApiError } from "../../api/apiErrors";
import { InlineAlert } from "../../components/InlineAlert";
import { useLanguage } from "../../i18n/LanguageProvider";

// The same three states for every office panel: loading, "sign in as staff", or the data.
export function OfficeGate<T>({ query, children }: { query: UseQueryResult<T>; children: (data: T) => ReactNode }) {
  const { t } = useLanguage();
  if (query.isPending) return <p className="loading-line">{t("common.loading")}</p>;
  if (query.isError) {
    if (isApiError(query.error) && query.error.status === 401) {
      return (
        <div className="sheet empty-state">
          <p>{t("office.staffOnly")}</p>
          <Link to="/staff/login" className="button button--secondary">
            <span>{t("account.signIn")}</span>
          </Link>
        </div>
      );
    }
    return <InlineAlert>{t(errorMessageKey(query.error))}</InlineAlert>;
  }
  return <>{children(query.data as T)}</>;
}
