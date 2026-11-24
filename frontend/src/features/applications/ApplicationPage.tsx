import { useMutation, useQuery } from "@tanstack/react-query";
import { useEffect, useState } from "react";
import { Link, useLocation, useNavigate, useParams } from "react-router";
import { admissionApi } from "../../api/admissionApiClient";
import { errorMessageKey, isApiError } from "../../api/apiErrors";
import type { HoldStatus } from "../../api/apiTypes";
import { Button } from "../../components/Button";
import { ConfirmDialog } from "../../components/ConfirmDialog";
import { InlineAlert } from "../../components/InlineAlert";
import { Spinner } from "../../components/Spinner";
import { useLanguage } from "../../i18n/LanguageProvider";
import type { MessageKey } from "../../i18n/en";
import { useServerNow } from "../../lib/serverClock";
import { useClassList } from "../classes/useClassList";
import { useStudentId } from "../student-identity/useStudentId";
import { ApplicantForm, applicationKey } from "./ApplicantForm";
import { DocumentUploads } from "./DocumentUploads";
import { PaymentReceipt } from "../payments/PaymentReceipt";
import { HoldCountdown } from "./HoldCountdown";
import { effectiveStatus } from "./holdStatus";
import { SeatToken } from "./SeatToken";
import { useApplyForSeat, useCancelHold, usePayFee, useRefreshSeats, useSeatHold } from "./useSeatHold";

export function ApplicationPage() {
  const { holdId = "" } = useParams();
  const location = useLocation();
  // Arriving straight from a successful payment plays the stamp once.
  const justPaid = (location.state as { justPaid?: boolean } | null)?.justPaid === true;
  // A new hold id means a new page: start its state from scratch.
  return <ApplicationView key={holdId} holdId={holdId} justPaid={justPaid} />;
}

function ApplicationView({ holdId, justPaid }: { holdId: string; justPaid: boolean }) {
  const { t } = useLanguage();
  const navigate = useNavigate();
  const { studentId, askForNumber } = useStudentId();
  const hold = useSeatHold(holdId);
  // Serial, fee and applicant details. Only the owner can read them.
  const application = useQuery({
    queryKey: applicationKey(holdId, studentId),
    queryFn: () => admissionApi.application(holdId, studentId!),
    enabled: studentId !== null,
    retry: false,
  });
  const classes = useClassList();
  const pay = usePayFee(holdId);
  // One key for this visit, so a double tap or a retry starts one payment, not two.
  const [paymentKey] = useState(() => crypto.randomUUID());
  const startPayment = useMutation({ mutationFn: (id: string) => admissionApi.startPayment(holdId, id, paymentKey) });
  const cancel = useCancelHold(holdId);
  const applyAgain = useApplyForSeat();
  const refreshSeats = useRefreshSeats();
  const now = useServerNow();

  const [cancelOpen, setCancelOpen] = useState(false);
  const [actionError, setActionError] = useState<MessageKey | null>(null);
  // Once this page has shown EXPIRED it never goes back to HELD, even if a slow
  // answer from before the expiry arrives late.
  const [expiredHere, setExpiredHere] = useState(false);
  const [expiryRechecks, setExpiryRechecks] = useState(0);

  const serverStatus = hold.data?.status;
  const refetchHold = hold.refetch;
  let status: HoldStatus | undefined = hold.data ? effectiveStatus(hold.data.status, hold.data.expiresAt, now) : undefined;
  if (expiredHere && status === "HELD") status = "EXPIRED";

  // The moment the countdown reaches zero: mark it, and refresh the seat numbers so the
  // freed seat shows everywhere.
  useEffect(() => {
    if (status === "EXPIRED" && serverStatus === "HELD" && !expiredHere) {
      setExpiredHere(true);
      refreshSeats();
    }
  }, [status, serverStatus, expiredHere, refreshSeats]);

  // Then ask the server again after about a second, and once more if it still says HELD.
  useEffect(() => {
    if (!expiredHere || serverStatus !== "HELD" || expiryRechecks >= 2) return;
    const timer = setTimeout(() => {
      setExpiryRechecks((count) => count + 1);
      void refetchHold();
    }, 1000);
    return () => clearTimeout(timer);
  }, [expiredHere, serverStatus, expiryRechecks, refetchHold]);

  if (hold.isPending) {
    return (
      <section className="page page--narrow" aria-busy="true">
        <p className="loading-line">
          <Spinner /> {t("application.loading")}
        </p>
      </section>
    );
  }

  if (!hold.data) {
    const notFound = isApiError(hold.error) && hold.error.status === 404;
    return (
      <section className="page page--narrow">
        <div className="sheet empty-state">
          <InlineAlert>{t(notFound ? "error.notFound" : errorMessageKey(hold.error))}</InlineAlert>
          {!notFound && (
            <Button variant="secondary" pending={hold.isFetching} onClick={() => void hold.refetch()}>
              {t("server.retry")}
            </Button>
          )}
          <Link to="/">{t("application.backToClasses")}</Link>
        </div>
      </section>
    );
  }

  const data = hold.data;
  const currentStatus = status ?? data.status;
  const seatsInClass = classes.data?.find((item) => item.id === data.classId);
  const className = data.className ?? seatsInClass?.name ?? "";
  const canPay = currentStatus === "HELD";

  function onPay() {
    setActionError(null);
    if (!studentId) {
      askForNumber();
      return;
    }
    // A class with a fee goes through the sandbox gateway. One with no fee confirms
    // directly through the original endpoint, as before.
    if ((application.data?.rules?.feeTaka ?? 0) > 0) {
      startPayment.mutate(studentId, {
        onSuccess: (started) => navigate(started.hostedPageUrl),
        onError: (error) => setActionError(errorMessageKey(error)),
      });
      return;
    }
    pay.mutate(studentId, { onError: (error) => setActionError(errorMessageKey(error)) });
  }

  function onCancelAsked() {
    setActionError(null);
    if (!studentId) {
      askForNumber();
      return;
    }
    setCancelOpen(true);
  }

  function onCancelConfirmed() {
    if (!studentId) return;
    cancel.mutate(studentId, {
      onSuccess: () => setCancelOpen(false),
      onError: (error) => {
        setCancelOpen(false);
        setActionError(errorMessageKey(error));
      },
    });
  }

  function onApplyAgain() {
    setActionError(null);
    if (!studentId) {
      askForNumber();
      return;
    }
    applyAgain.mutate(
      { classId: data.classId, studentId },
      {
        onSuccess: (next) => navigate(`/applications/${next.holdId}`),
        onError: (error) => setActionError(errorMessageKey(error)),
      },
    );
  }

  return (
    <section className="page page--narrow">
      <h1 className="page-title">{t("application.title")}</h1>

      <SeatToken
        stampLanding={justPaid}
        className={className}
        serial={application.data?.serial ?? data.serial}
        status={currentStatus} studentId={studentId} createdAt={data.createdAt}>
        {currentStatus === "HELD" && <HoldCountdown expiresAt={data.expiresAt} startedAt={data.createdAt} now={now} />}
        {currentStatus === "CONFIRMED" && (
          <>
            <p className="token__note token__note--confirmed">{t("application.confirmedNote")}</p>
            <Link to={`/applications/${holdId}/slip`} className="button button--secondary token__print">
              <span>{t("application.print")}</span>
            </Link>
          </>
        )}
        {currentStatus === "EXPIRED" && <p className="token__note">{t("application.expiredNote")}</p>}
        {currentStatus === "CANCELLED" && <p className="token__note">{t("application.cancelledNote")}</p>}
      </SeatToken>

      {application.data && <PaymentReceipt application={application.data} />}

      {currentStatus === "HELD" && application.data && studentId && (
        <>
          <ApplicantForm key={application.data.holdId} application={application.data} studentId={studentId} />
          <DocumentUploads application={application.data} studentId={studentId} />
        </>
      )}

      <div className="token-actions" aria-live="polite">
        {(currentStatus === "HELD" || currentStatus === "EXPIRED") && (
          <div className="token-actions__pay">
            <Button wide pending={pay.isPending || startPayment.isPending} disabled={!canPay} onClick={onPay}>
              {t("application.pay")}
            </Button>
            <p className="token-actions__note">{t("application.payNote")}</p>
          </div>
        )}
        {currentStatus === "HELD" && (
          <Button variant="secondary" onClick={onCancelAsked} disabled={pay.isPending}>
            {t("application.cancel")}
          </Button>
        )}
        {(currentStatus === "EXPIRED" || currentStatus === "CANCELLED") &&
          (seatsInClass && seatsInClass.availableSeats > 0 ? (
            <Button variant="secondary" pending={applyAgain.isPending} onClick={onApplyAgain}>
              {t("application.applyAgain")}
            </Button>
          ) : (
            seatsInClass && <p className="token-actions__note">{t("application.noSeatsToApplyAgain")}</p>
          ))}
        {actionError && <InlineAlert>{t(actionError)}</InlineAlert>}
      </div>

      <p className="back-link">
        <Link to="/">{t("application.backToClasses")}</Link>
      </p>

      <ConfirmDialog
        open={cancelOpen}
        question={t("application.cancelQuestion")}
        confirmLabel={t("application.cancelYes")}
        cancelLabel={t("application.cancelNo")}
        pending={cancel.isPending}
        onConfirm={onCancelConfirmed}
        onClose={() => setCancelOpen(false)}
      />
    </section>
  );
}
