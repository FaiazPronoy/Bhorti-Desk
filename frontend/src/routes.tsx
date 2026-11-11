import type { RouteObject } from "react-router";
import { App } from "./App";
import { NotFoundPage } from "./components/NotFoundPage";
import { ApplicationPage } from "./features/applications/ApplicationPage";
import { MyApplicationsPage } from "./features/applications/MyApplicationsPage";
import { ClassListPage } from "./features/classes/ClassListPage";
import { CreateClassPage } from "./features/school-office/CreateClassPage";

export const routes: RouteObject[] = [
  {
    path: "/",
    element: <App />,
    children: [
      { index: true, element: <ClassListPage /> },
      { path: "applications", element: <MyApplicationsPage /> },
      { path: "applications/:holdId", element: <ApplicationPage /> },
      { path: "school-office", element: <CreateClassPage /> },
      { path: "*", element: <NotFoundPage /> },
    ],
  },
];
