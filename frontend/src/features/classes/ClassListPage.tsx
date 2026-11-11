import { useState } from "react";
import { Link, useNavigate } from "react-router";
import { errorMessageKey, isApiError } from "../../api/apiErrors";
import type { ClassSeatSummary } from "../../api/apiTypes";
import { Button } from "../../components/Button";
import { InlineAlert } from "../../components/InlineAlert";
import { useLanguage } from "../../i18n/LanguageProvider";
import type { MessageKey } from "../../i18n/en";
import { effectiveStatus } from "../applications/holdStatus";
import { useMyHolds } from "../applications/useMyHolds";
import { useApplyForSeat } from "../applications/useSeatHold";
import { useStudentId } from "../student-identity/useStudentId";
import { serverNow } from "../../lib/serverClock";
import { ClassRegisterRow } from "./ClassRegisterRow";
import { useClassList } from "./useClassList";

interface RowError {
  classId: string;
  key: MessageKey;
  holdId?: string;
}

export function ClassListPage() {
  const { t } = useLanguage();

  return (
    <section className="page">
      <h1 className="page-title">{t("classes.title")}</h1>
      <p className="page-intro">{t("classes.intro")}</p>
      <ClassRegister showActions />
    </section>
  );
}

// The register of classes. Used on the class list (with Apply) and in the school
// office (numbers only).
export function ClassRegister({ showActions }: { showActions: boolean }) {
  const { t } = useLanguage();
  const navigate = useNavigate();
  const classes = useClassList();
  const myHolds = useMyHolds();
  const { studentId, askForNumber } = useStudentId();
  const apply = useApplyForSeat();
  const [rowError, setRowError] = useState<RowError | null>(null);

  function activeHoldFor(classId: string) {
    const now = serverNow();
    return myHolds.data?.find((hold) => {
      if (hold.classId !== classId) return false;
      const status = effectiveStatus(hold.status, hold.expiresAt, now);
      return status === "HELD" || status === "CONFIRMED";
    });
  }

  function onApply(seats: ClassSeatSummary) {
    setRowError(null);
    if (!studentId) {
      askForNumber();
      return;
    }
    apply.mutate(
      { classId: seats.id, studentId },
      {
        onSuccess: (hold) => navigate(`/applications/${hold.holdId}`),
        onError: (error) => {
          setRowError({
            classId: seats.id,
            key: errorMessageKey(error),
            holdId: isApiError(error) ? error.holdId : undefined,
          });
          if (isApiError(error) && (error.code === "STUDENT_ID_INVALID" || error.code === "STUDENT_ID_REQUIRED")) {
            askForNumber();
          }
        },
      },
    );
  }

  if (classes.isPending) {
    return (
      <div className="register register--loading" aria-busy="true" aria-label={t("classes.loading")}>
        {[0, 1, 2].map((row) => (
          <div key={row} className="skeleton-row" />
        ))}
      </div>
    );
  }

  if (classes.isError && !classes.data) {
    return (
      <div className="sheet empty-state">
        <InlineAlert>{t(errorMessageKey(classes.error))}</InlineAlert>
        <Button variant="secondary" pending={classes.isFetching} onClick={() => void classes.refetch()}>
          {t("server.retry")}
        </Button>
      </div>
    );
  }

  const list = classes.data ?? [];

  if (list.length === 0) {
    return (
      <div className="sheet empty-state">
        <h2 className="section-title">{t("classes.emptyTitle")}</h2>
        <p>{t("classes.emptyBody")}</p>
        {showActions && (
          <Link to="/school-office" className="button button--primary">
            <span>{t("classes.emptyAction")}</span>
          </Link>
        )}
      </div>
    );
  }

  return (
    <div className={`register ${classes.isError ? "register--stale" : ""}`}>
      <div className={`register__head ${showActions ? "" : "register__head--office"}`} aria-hidden="true">
        <span>{t("classes.name")}</span>
        <span>{t("classes.seats")}</span>
        <span>{t("classes.total")}</span>
        <span>{t("classes.onHold")}</span>
        <span>{t("classes.confirmed")}</span>
        <span>{t("classes.free")}</span>
        {showActions && <span>{t("classes.action")}</span>}
      </div>
      <ul className="register__rows">
        {list.map((seats) => (
          <ClassRegisterRow
            key={seats.id}
            seats={seats}
            showActions={showActions}
            myActiveHold={activeHoldFor(seats.id)}
            applying={apply.isPending && apply.variables?.classId === seats.id}
            applyDisabled={apply.isPending}
            errorKey={rowError?.classId === seats.id ? rowError.key : undefined}
            errorHoldId={rowError?.classId === seats.id ? rowError.holdId : undefined}
            onApply={() => onApply(seats)}
          />
        ))}
      </ul>
    </div>
  );
}
