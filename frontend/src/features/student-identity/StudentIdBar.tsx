import { useId } from "react";
import { Link, useNavigate } from "react-router";
import { admissionApi } from "../../api/admissionApiClient";
import { useLanguage } from "../../i18n/LanguageProvider";
import { useMe, useResetAccount } from "../account/useAccount";
import { useStudentId } from "./useStudentId";

// The strip under the header: who is signed in, and which child they are applying for.
export function StudentIdBar() {
  const { t } = useLanguage();
  const me = useMe();
  const { studentId, students, selectStudent } = useStudentId();
  const resetAccount = useResetAccount();
  const navigate = useNavigate();
  const pickerId = useId();

  async function signOut() {
    try {
      await admissionApi.logout();
    } finally {
      await resetAccount();
      navigate("/");
    }
  }

  if (me.isPending) return <div className="student-bar student-bar--loading" aria-hidden="true" />;

  if (!me.data) {
    return (
      <div className="student-bar">
        <div className="student-bar__row">
          <Link to="/login" className="button button--primary">
            <span>{t("students.signInToApply")}</span>
          </Link>
          <p className="student-bar__help">{t("student.help")}</p>
        </div>
      </div>
    );
  }

  if (me.data.kind === "STAFF") {
    return (
      <div className="student-bar">
        <div className="student-bar__row">
          <span className="student-bar__label">{t("nav.schoolOffice")}</span>
          <button type="button" className="button button--quiet" onClick={signOut}>
            {t("account.signOut")}
          </button>
        </div>
      </div>
    );
  }

  return (
    <div className="student-bar student-bar--saved">
      <div className="student-bar__row">
        {students.length > 0 ? (
          <>
            <label htmlFor={pickerId} className="student-bar__label">
              {t("students.applyingFor")}
            </label>
            <select
              id={pickerId}
              className="student-bar__input student-bar__select"
              value={studentId ?? ""}
              onChange={(event) => selectStudent(event.target.value)}
            >
              {students.map((student) => (
                <option key={student.id} value={student.birthRegistrationNumber}>
                  {student.fullName} ({student.birthRegistrationNumber.slice(-4)})
                </option>
              ))}
            </select>
          </>
        ) : (
          <p className="student-bar__label">{t("students.empty")}</p>
        )}
        <div className="student-bar__field">
          <Link to="/students" className="button button--quiet">
            {t("students.manage")}
          </Link>
          <button type="button" className="button button--quiet" onClick={signOut}>
            {t("account.signOut")}
          </button>
        </div>
      </div>
      <details className="student-bar__more">
        <summary>{t("account.sharedComputerQuestion")}</summary>
        <p>{t("account.sharedComputer")}</p>
      </details>
    </div>
  );
}
