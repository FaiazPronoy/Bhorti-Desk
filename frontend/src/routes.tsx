import type { RouteObject } from "react-router";
import { App } from "./App";
import { NotFoundPage } from "./components/NotFoundPage";
import { ApplicationPage } from "./features/applications/ApplicationPage";
import { MyApplicationsPage } from "./features/applications/MyApplicationsPage";
import { ClassListPage } from "./features/classes/ClassListPage";
import { CreateClassPage } from "./features/school-office/CreateClassPage";
import { LoginPage } from "./features/account/LoginPage";
import { StaffLoginPage } from "./features/account/StaffLoginPage";
import { StudentsPage } from "./features/account/StudentsPage";
import { SandboxPayPage } from "./features/payments/SandboxPayPage";
import { SlipPage } from "./features/slip/SlipPage";
import { VerifyPage } from "./features/slip/VerifyPage";

export const routes: RouteObject[] = [
  {
    path: "/",
    element: <App />,
    children: [
      { index: true, element: <ClassListPage /> },
      { path: "applications", element: <MyApplicationsPage /> },
      { path: "applications/:holdId", element: <ApplicationPage /> },
      { path: "school-office", element: <CreateClassPage /> },
      { path: "login", element: <LoginPage /> },
      { path: "staff/login", element: <StaffLoginPage /> },
      { path: "students", element: <StudentsPage /> },
      { path: "pay/:paymentId", element: <SandboxPayPage /> },
      { path: "applications/:holdId/slip", element: <SlipPage /> },
      { path: "verify/:code", element: <VerifyPage /> },
      { path: "*", element: <NotFoundPage /> },
    ],
  },
];
