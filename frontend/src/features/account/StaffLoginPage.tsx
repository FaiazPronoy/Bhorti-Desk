import { useState, type FormEvent } from "react";
import { Link, useNavigate } from "react-router";
import { admissionApi } from "../../api/admissionApiClient";
import { errorMessageKey } from "../../api/apiErrors";
import { Button } from "../../components/Button";
import { InlineAlert } from "../../components/InlineAlert";
import { TextField } from "../../components/TextField";
import { useLanguage } from "../../i18n/LanguageProvider";
import type { MessageKey } from "../../i18n/en";
import { useResetAccount } from "./useAccount";

export function StaffLoginPage() {
  const { t } = useLanguage();
  const navigate = useNavigate();
  const resetAccount = useResetAccount();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [pending, setPending] = useState(false);
  const [error, setError] = useState<MessageKey | null>(null);

  async function signIn(event: FormEvent) {
    event.preventDefault();
    setError(null);
    setPending(true);
    try {
      await admissionApi.staffLogin(email.trim(), password);
      await resetAccount();
      navigate("/school-office", { replace: true });
    } catch (caught) {
      setError(errorMessageKey(caught));
      setPassword("");
    } finally {
      setPending(false);
    }
  }

  return (
    <section className="page page--narrow">
      <h1 className="page-title">{t("account.staffTitle")}</h1>
      <form className="sheet office-form" onSubmit={signIn} noValidate>
        <TextField label={t("account.email")} type="email" autoComplete="username" spellCheck={false}
          value={email} onChange={(event) => setEmail(event.target.value)} />
        <TextField label={t("account.password")} type="password" autoComplete="current-password"
          value={password} onChange={(event) => setPassword(event.target.value)} />
        <Button type="submit" pending={pending}>
          {t("account.signIn")}
        </Button>
        {error && <InlineAlert>{t(error)}</InlineAlert>}
      </form>
      <p className="back-link">
        <Link to="/login">{t("account.guardianLink")}</Link>
      </p>
    </section>
  );
}
