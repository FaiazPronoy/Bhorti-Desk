import { useQuery } from "@tanstack/react-query";
import { admissionApi } from "../../api/admissionApiClient";
import { useLanguage } from "../../i18n/LanguageProvider";
import { SeatBar } from "../classes/SeatBar";
import { FunnelBars, HourlyBars } from "./charts";
import { OfficeGate } from "./OfficeGate";

export function OfficeDashboard() {
  const { t, number } = useLanguage();
  const report = useQuery({ queryKey: ["office", "report"], queryFn: admissionApi.officeReport, retry: false, refetchInterval: 30_000 });

  return (
    <OfficeGate query={report}>
      {(data) => (
        <div className="dashboard">
          <section className="dashboard__main">
            <h3 className="section-title">{t("dashboard.perHour")}</h3>
            <HourlyBars hours={data.applicationsPerHour} />
          </section>
          <section>
            <h3 className="section-title">{t("dashboard.funnel")}</h3>
            <FunnelBars
              stages={[
                { label: t("dashboard.applied"), value: data.funnel.applied },
                { label: t("dashboard.paid"), value: data.funnel.paid },
                { label: t("dashboard.verified"), value: data.funnel.verified },
              ]}
            />
            <p className="dashboard__note">
              {t("dashboard.onHoldNow", { count: number(data.funnel.onHoldNow) })} {t("dashboard.rejected", { count: number(data.funnel.rejected) })}
            </p>
            <p className="dashboard__note">{t("dashboard.revenue", { amount: number(data.sandboxRevenueTaka) })}</p>
          </section>
          <section className="dashboard__wide">
            <div className="dashboard__head">
              <h3 className="section-title">{t("dashboard.fill")}</h3>
              <a className="button button--quiet" href={admissionApi.csvUrl("/office/reports/classes.csv")}>
                {t("dashboard.csv")}
              </a>
            </div>
            <table className="fill-table">
              <thead>
                <tr>
                  <th scope="col">{t("classes.name")}</th>
                  <th scope="col">{t("classes.seats")}</th>
                  <th scope="col">{t("dashboard.inUse")}</th>
                  <th scope="col">{t("dashboard.waiting")}</th>
                </tr>
              </thead>
              <tbody>
                {data.classes.map((item) => (
                  <tr key={item.id}>
                    <th scope="row">
                      {item.name}
                      {item.closed && <span className="fill-table__closed"> {t("officeClasses.closed")}</span>}
                    </th>
                    <td>
                      <SeatBar seats={{ id: item.id, name: item.name, totalSeats: item.totalSeats, heldSeats: 0, confirmedSeats: item.inUse, availableSeats: item.totalSeats - item.inUse }} />
                    </td>
                    <td>
                      {number(item.inUse)} / {number(item.totalSeats)}
                    </td>
                    <td>{number(item.waiting)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </section>
        </div>
      )}
    </OfficeGate>
  );
}
