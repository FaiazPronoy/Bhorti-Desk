import { useLanguage } from "../../i18n/LanguageProvider";
import { ClassRegister } from "../classes/ClassListPage";
import { CreateClassForm } from "./CreateClassForm";

export function CreateClassPage() {
  const { t } = useLanguage();
  return (
    <section className="page">
      <h1 className="page-title">{t("office.title")}</h1>
      <p className="page-intro">{t("office.intro")}</p>
      <div className="office-layout">
        <CreateClassForm />
        <div>
          <h2 className="section-title">{t("office.liveTable")}</h2>
          <ClassRegister showActions={false} />
        </div>
      </div>
    </section>
  );
}
