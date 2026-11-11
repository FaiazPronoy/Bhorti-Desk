import { Link } from "react-router";
import type { ClassSeatSummary, StudentHold } from "../../api/apiTypes";
import { Button } from "../../components/Button";
import { useLanguage } from "../../i18n/LanguageProvider";
import type { MessageKey } from "../../i18n/en";
import { SeatBar } from "./SeatBar";
import { SEAT_GRID_LIMIT, SeatGrid } from "./SeatGrid";

interface ClassRegisterRowProps {
  seats: ClassSeatSummary;
  // Office view shows the numbers only, with no Apply button.
  showActions: boolean;
  myActiveHold?: StudentHold;
  applying: boolean;
  applyDisabled: boolean;
  errorKey?: MessageKey;
  errorHoldId?: string;
  onApply: () => void;
}

// One line of the register. From 768 px up the cells line up under the column heads;
// below that the same cells stack into a small sheet with their own labels.
export function ClassRegisterRow({ seats, showActions, myActiveHold, applying, applyDisabled, errorKey, errorHoldId, onApply }: ClassRegisterRowProps) {
  const { t, number } = useLanguage();
  const isFull = seats.availableSeats <= 0;

  const figures: { label: MessageKey; value: number; className: string }[] = [
    { label: "classes.total", value: seats.totalSeats, className: "" },
    { label: "classes.onHold", value: seats.heldSeats, className: "figure--held" },
    { label: "classes.confirmed", value: seats.confirmedSeats, className: "figure--confirmed" },
    { label: "classes.free", value: seats.availableSeats, className: "figure--free" },
  ];

  return (
    <li className={`register-row ${showActions ? "" : "register-row--office"}`}>
      <div className="register-row__name">
        <h3 className="register-row__title">{seats.name}</h3>
        <p className="register-row__free-big">{t("classes.freeSeats", { count: number(seats.availableSeats) })}</p>
      </div>

      <div className="register-row__picture">
        {seats.totalSeats <= SEAT_GRID_LIMIT ? (
          <>
            <div className="only-wide">
              <SeatGrid seats={seats} />
            </div>
            <div className="only-narrow">
              <SeatBar seats={seats} />
            </div>
          </>
        ) : (
          <SeatBar seats={seats} />
        )}
      </div>

      <dl className="register-row__figures">
        {figures.map((figure) => (
          <div key={figure.label} className={`figure ${figure.className}`}>
            <dt className="figure__label">{t(figure.label)}</dt>
            <dd className="figure__value">{number(figure.value)}</dd>
          </div>
        ))}
      </dl>

      {showActions && (
        <div className="register-row__action">
          {myActiveHold ? (
            <div className="register-row__applied">
              <span className="register-row__applied-label">{t("classes.applied")}</span>
              <Link to={`/applications/${myActiveHold.holdId}`}>{t("classes.openApplication")}</Link>
            </div>
          ) : (
            <Button wide pending={applying} disabled={isFull || applyDisabled} onClick={onApply}>
              {isFull ? t("classes.full") : t("classes.apply")}
            </Button>
          )}
          {errorKey && (
            <p className="action-error" role="alert">
              {t(errorKey)}{" "}
              {errorHoldId && <Link to={`/applications/${errorHoldId}`}>{t("classes.openApplication")}</Link>}
            </p>
          )}
        </div>
      )}
    </li>
  );
}
