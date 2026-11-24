import { HowItWorks } from "../../components/HowItWorks";
import { useState, type FormEvent } from "react";
import { Link, useLocation, useNavigate } from "react-router";
import { admissionApi } from "../../api/admissionApiClient";
import { errorMessageKey } from "../../api/apiErrors";
import { Button } from "../../components/Button";
import { InlineAlert } from "../../components/InlineAlert";
import { TextField } from "../../components/TextField";
import { useLanguage } from "../../i18n/LanguageProvider";
import type { MessageKey } from "../../i18n/en";
import { toLatinDigits } from "../student-identity/birthRegistrationNumber";
import { useResetAccount } from "./useAccount";

// Guardian sign-in: a mobile number, then the 6 digit code sent to it.
export function LoginPage() {
  const { t } = useLanguage();
  const navigate = useNavigate();
  const location = useLocation();
  const resetAccount = useResetAccount();
  const [phone, setPhone] = useState("");
  const [code, setCode] = useState("");
  const [step, setStep] = useState<"phone" | "code">("phone");
  const [pending, setPending] = useState(false);
  const [error, setError] = useState<MessageKey | null>(null);

  const cleanPhone = toLatinDigits(phone).replace(/[^0-9+]/g, "");
  const from = (location.state as { from?: string } | null)?.from;

  async function sendCode(event: FormEvent) {
    event.preventDefault();
    setError(null);
    setPending(true);
    try {
      await admissionApi.requestOtp(cleanPhone);
      setStep("code");
    } catch (caught) {
      setError(errorMessageKey(caught));
    } finally {
      setPending(false);
    }
  }

  async function verify(event: FormEvent) {
    event.preventDefault();
    setError(null);
    setPending(true);
    try {
      await admissionApi.verifyOtp(cleanPhone, toLatinDigits(code).replace(/\D/g, ""));
      await resetAccount();
      navigate(from ?? "/students", { replace: true });
    } catch (caught) {
      setError(errorMessageKey(caught));
    } finally {
      setPending(false);
    }
  }

  return (
    <section className="page page--narrow">
      <h1 className="page-title">{t("account.signInTitle")}</h1>
      <HowItWorks text={t("account.signInIntro")} />

      {step === "phone" ? (
        <form className="sheet office-form" onSubmit={sendCode} noValidate>
          <TextField
            label={t("account.phone")}
            hint={t("account.phoneHint")}
            type="tel"
            inputMode="tel"
            autoComplete="tel"
            maxLength={16}
            value={phone}
            onChange={(event) => setPhone(event.target.value)}
          />
          <Button type="submit" pending={pending}>
            {t("account.sendCode")}
          </Button>
          {error && <InlineAlert>{t(error)}</InlineAlert>}
        </form>
      ) : (
        <form className="sheet office-form" onSubmit={verify} noValidate>
          <InlineAlert tone="info">{t("account.codeSent", { phone: cleanPhone })}</InlineAlert>
          <TextField
            label={t("account.code")}
            inputMode="numeric"
            autoComplete="one-time-code"
            maxLength={6}
            value={code}
            onChange={(event) => setCode(event.target.value)}
            className="field--short"
          />
          <Button type="submit" pending={pending}>
            {t("account.verify")}
          </Button>
          <Button variant="quiet" onClick={() => { setStep("phone"); setCode(""); setError(null); }}>
            {t("account.changePhone")}
          </Button>
          {error && <InlineAlert>{t(error)}</InlineAlert>}
        </form>
      )}

      <p className="back-link">
        <Link to="/staff/login">{t("account.staffLink")}</Link>
      </p>
    </section>
  );
}
