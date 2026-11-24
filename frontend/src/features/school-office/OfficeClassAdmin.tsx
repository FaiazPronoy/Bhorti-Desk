import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { admissionApi } from "../../api/admissionApiClient";
import { errorMessageKey, isApiError } from "../../api/apiErrors";
import type { OfficeClass } from "../../api/apiTypes";
import { Button } from "../../components/Button";
import { InlineAlert } from "../../components/InlineAlert";
import { TextField } from "../../components/TextField";
import { useLanguage } from "../../i18n/LanguageProvider";
import { en, type MessageKey } from "../../i18n/en";
import { classListKey } from "../classes/useClassList";
import { OfficeGate } from "./OfficeGate";

const officeClassesKey = ["office", "classes"] as const;

export function OfficeClassAdmin() {
  const classes = useQuery({ queryKey: officeClassesKey, queryFn: admissionApi.officeClasses, retry: false });
  return (
    <OfficeGate query={classes}>
      {(data) => (
        <ul className="review-list">
          {data.map((item) => (
            <ClassAdminRow key={item.id} item={item} />
          ))}
        </ul>
      )}
    </OfficeGate>
  );
}

function ClassAdminRow({ item }: { item: OfficeClass }) {
  const { t, number } = useLanguage();
  const queryClient = useQueryClient();
  const [editing, setEditing] = useState(false);
  const [name, setName] = useState(item.name);
  const [seats, setSeats] = useState(String(item.totalSeats));
  const [error, setError] = useState<string | null>(null);
  const refresh = () => {
    setError(null);
    void queryClient.invalidateQueries({ queryKey: officeClassesKey });
    void queryClient.invalidateQueries({ queryKey: classListKey });
  };
  const fail = (caught: unknown) => {
    const field = isApiError(caught) ? caught.fieldErrors.totalSeats ?? caught.fieldErrors.name : undefined;
    setError(field && field in en ? t(field as MessageKey, { inUse: number(item.inUse) }) : t(errorMessageKey(caught)));
  };
  const save = useMutation({
    mutationFn: () => admissionApi.editClass(item.id, name.trim(), Number(seats)),
    onSuccess: () => {
      setEditing(false);
      refresh();
    },
    onError: fail,
  });
  const toggle = useMutation({ mutationFn: () => admissionApi.setClassClosed(item.id, !item.closed), onSuccess: refresh, onError: fail });

  return (
    <li className="review-row">
      <div className="review-row__head">
        <div>
          <h3 className="application-row__class">{item.name}</h3>
          <p className="application-row__date">
            {t("officeClasses.summary", { inUse: number(item.inUse), total: number(item.totalSeats), waiting: number(item.waiting) })}
            {item.closed && `, ${t("officeClasses.closed")}`}
          </p>
        </div>
        {!editing && (
          <div className="review-row__actions">
            <Button variant="secondary" onClick={() => setEditing(true)}>
              {t("officeClasses.edit")}
            </Button>
            <Button variant="quiet" pending={toggle.isPending} onClick={() => toggle.mutate()}>
              {item.closed ? t("officeClasses.reopen") : t("officeClasses.close")}
            </Button>
          </div>
        )}
      </div>
      {editing && (
        <div className="review-row__reject">
          <TextField label={t("office.className")} maxLength={100} value={name} onChange={(event) => setName(event.target.value)} />
          <TextField
            label={t("office.seats")}
            hint={t("officeClasses.seatsHint", { inUse: number(item.inUse) })}
            inputMode="numeric"
            maxLength={6}
            className="field--short"
            value={seats}
            onChange={(event) => setSeats(event.target.value.replace(/\D/g, ""))}
          />
          <div className="review-row__actions">
            <Button pending={save.isPending} onClick={() => save.mutate()}>
              {t("officeClasses.save")}
            </Button>
            <Button variant="quiet" onClick={() => setEditing(false)}>
              {t("application.cancelNo")}
            </Button>
          </div>
        </div>
      )}
      {error && <InlineAlert>{error}</InlineAlert>}
    </li>
  );
}
