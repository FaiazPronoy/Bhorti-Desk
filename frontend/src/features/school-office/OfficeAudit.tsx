import { useQuery } from "@tanstack/react-query";
import { useState } from "react";
import { admissionApi } from "../../api/admissionApiClient";
import { useLanguage } from "../../i18n/LanguageProvider";
import { formatDateTime } from "../../i18n/formatNumber";
import { OfficeGate } from "./OfficeGate";

const actions = [
  "SESSION_CREATED", "SESSION_PUBLISHED", "SESSION_CLOSED", "SESSION_CLASS_ADDED", "CLASS_RULES_SAVED",
  "CLASS_EDITED", "CLASS_CLOSED", "CLASS_REOPENED", "APPLICATION_VERIFIED", "APPLICATION_REJECTED", "DOCUMENT_VIEWED",
];

export function OfficeAudit() {
  const { t, language } = useLanguage();
  const [action, setAction] = useState("");
  const audit = useQuery({ queryKey: ["office", "audit", action], queryFn: () => admissionApi.officeAudit(action), retry: false });

  return (
    <div className="audit">
      <div className="audit__filters">
        <label htmlFor="audit-action" className="field__label">
          {t("audit.filter")}
        </label>
        <select id="audit-action" className="field__input field__select" value={action} onChange={(event) => setAction(event.target.value)}>
          <option value="">{t("audit.all")}</option>
          {actions.map((name) => (
            <option key={name} value={name}>
              {name}
            </option>
          ))}
        </select>
        <a className="button button--quiet" href={admissionApi.csvUrl("/office/audit.csv", action)}>
          {t("dashboard.csv")}
        </a>
      </div>
      <OfficeGate query={audit}>
        {(entries) =>
          entries.length === 0 ? (
            <p className="sheet empty-state">{t("audit.empty")}</p>
          ) : (
            <table className="fill-table">
              <thead>
                <tr>
                  <th scope="col">{t("payment.when")}</th>
                  <th scope="col">{t("audit.action")}</th>
                  <th scope="col">{t("audit.target")}</th>
                  <th scope="col">{t("audit.detail")}</th>
                </tr>
              </thead>
              <tbody>
                {entries.map((entry) => (
                  <tr key={entry.id}>
                    <td>{formatDateTime(entry.createdAt, language)}</td>
                    <td className="mono">{entry.action}</td>
                    <td className="mono">
                      {entry.targetType} {entry.targetId.slice(-6)}
                    </td>
                    <td>{entry.detail}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )
        }
      </OfficeGate>
    </div>
  );
}
