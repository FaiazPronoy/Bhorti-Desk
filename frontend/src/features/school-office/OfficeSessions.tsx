import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState, type FormEvent } from "react";
import { Link } from "react-router";
import { admissionApi } from "../../api/admissionApiClient";
import { errorMessageKey, isApiError } from "../../api/apiErrors";
import type { AdmissionSession } from "../../api/apiTypes";
import { Button } from "../../components/Button";
import { InlineAlert } from "../../components/InlineAlert";
import { TextField } from "../../components/TextField";
import { useLanguage } from "../../i18n/LanguageProvider";
import type { MessageKey } from "../../i18n/en";
import { formatDateTime } from "../../i18n/formatNumber";
import { useClassList } from "../classes/useClassList";
import { seasonKey } from "../season/useSeason";
import { OfficeClassRules } from "./OfficeClassRules";

const officeSessionsKey = ["office", "sessions"] as const;

export function OfficeSessions() {
  const { t } = useLanguage();
  const sessions = useQuery({ queryKey: officeSessionsKey, queryFn: admissionApi.officeSessions, retry: false });

  if (sessions.isPending) return <p className="loading-line">{t("common.loading")}</p>;
  if (sessions.isError) {
    if (isApiError(sessions.error) && sessions.error.status === 401) {
      return (
        <div className="sheet empty-state">
          <p>{t("office.sessionsNeedSignIn")}</p>
          <Link to="/staff/login" className="button button--secondary">
            <span>{t("account.signIn")}</span>
          </Link>
        </div>
      );
    }
    return <InlineAlert>{t(errorMessageKey(sessions.error))}</InlineAlert>;
  }

  return (
    <div className="office-sessions">
      {sessions.data.length > 0 && (
        <ul className="applications">
          {sessions.data.map((session) => (
            <SessionRow key={session.id} session={session} />
          ))}
        </ul>
      )}
      <NewSessionForm />
      <OfficeClassRules />
    </div>
  );
}

function useRefreshSessions() {
  const queryClient = useQueryClient();
  return () => {
    void queryClient.invalidateQueries({ queryKey: officeSessionsKey });
    void queryClient.invalidateQueries({ queryKey: seasonKey });
  };
}

function SessionRow({ session }: { session: AdmissionSession }) {
  const { t, language, number } = useLanguage();
  const classes = useClassList();
  const refresh = useRefreshSessions();
  const [classId, setClassId] = useState("");
  const [error, setError] = useState<MessageKey | null>(null);
  const onError = (caught: unknown) => setError(errorMessageKey(caught));
  const onSuccess = () => {
    setError(null);
    setClassId("");
    refresh();
  };
  const publish = useMutation({ mutationFn: () => admissionApi.publishSession(session.id), onSuccess, onError });
  const close = useMutation({ mutationFn: () => admissionApi.closeSession(session.id), onSuccess, onError });
  const attach = useMutation({ mutationFn: () => admissionApi.addClassToSession(session.id, classId), onSuccess, onError });
  const available = (classes.data ?? []).filter((item) => !session.classIds.includes(item.id));

  return (
    <li className="application-row office-session">
      <div className="application-row__main">
        <h3 className="application-row__class">{session.name}</h3>
        <p className="application-row__date">
          {formatDateTime(session.opensAt, language)} {t("office.sessionTo")} {formatDateTime(session.closesAt, language)}
        </p>
        <p className="application-row__date">
          {t(`office.sessionStatus.${session.status}`)}, {t("office.sessionClasses", { count: number(session.classIds.length) })}
        </p>
      </div>
      <div className="office-session__actions">
        {session.status !== "CLOSED" && available.length > 0 && (
          <>
            <label className="sr-only" htmlFor={`add-class-${session.id}`}>
              {t("office.sessionAddClass")}
            </label>
            <select id={`add-class-${session.id}`} className="field__input field__select" value={classId} onChange={(event) => setClassId(event.target.value)}>
              <option value="">{t("office.sessionChooseClass")}</option>
              {available.map((item) => (
                <option key={item.id} value={item.id}>
                  {item.name}
                </option>
              ))}
            </select>
            <Button variant="secondary" disabled={!classId} pending={attach.isPending} onClick={() => attach.mutate()}>
              {t("office.sessionAddClass")}
            </Button>
          </>
        )}
        {session.status === "DRAFT" && (
          <Button variant="secondary" pending={publish.isPending} onClick={() => publish.mutate()}>
            {t("office.sessionPublish")}
          </Button>
        )}
        {session.status === "PUBLISHED" && (
          <Button variant="secondary" pending={close.isPending} onClick={() => close.mutate()}>
            {t("office.sessionClose")}
          </Button>
        )}
      </div>
      {error && <InlineAlert>{t(error)}</InlineAlert>}
    </li>
  );
}

function NewSessionForm() {
  const { t } = useLanguage();
  const refresh = useRefreshSessions();
  const [year, setYear] = useState("");
  const [name, setName] = useState("");
  const [opensAt, setOpensAt] = useState("");
  const [closesAt, setClosesAt] = useState("");
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});
  const [formError, setFormError] = useState<MessageKey | null>(null);

  const create = useMutation({
    mutationFn: admissionApi.createSession,
    onSuccess: () => {
      setYear("");
      setName("");
      setOpensAt("");
      setClosesAt("");
      setFieldErrors({});
      refresh();
    },
    onError: (error) => {
      setFieldErrors(isApiError(error) ? error.fieldErrors : {});
      setFormError(errorMessageKey(error));
    },
  });

  function onSubmit(event: FormEvent) {
    event.preventDefault();
    setFormError(null);
    // The picker gives local time with no zone. Turned into an exact moment here.
    const exact = (value: string) => (value ? new Date(value).toISOString() : null);
    create.mutate({ academicYear: year ? Number(year) : null, name: name.trim(), opensAt: exact(opensAt), closesAt: exact(closesAt) });
  }

  return (
    <form className="sheet office-form" onSubmit={onSubmit} noValidate>
      <h3 className="section-title">{t("office.sessionNew")}</h3>
      <TextField label={t("office.sessionName")} maxLength={100} value={name} onChange={(event) => setName(event.target.value)} error={fieldErrors.name} />
      <TextField label={t("office.sessionYear")} inputMode="numeric" maxLength={4} className="field--short" value={year} onChange={(event) => setYear(event.target.value.replace(/\D/g, ""))} error={fieldErrors.academicYear} />
      <TextField label={t("office.sessionOpens")} type="datetime-local" value={opensAt} onChange={(event) => setOpensAt(event.target.value)} error={fieldErrors.opensAt} />
      <TextField label={t("office.sessionCloses")} type="datetime-local" value={closesAt} onChange={(event) => setClosesAt(event.target.value)} error={fieldErrors.closesAt} />
      <Button type="submit" pending={create.isPending}>
        {t("office.sessionCreate")}
      </Button>
      {formError && <InlineAlert>{t(formError)}</InlineAlert>}
    </form>
  );
}
