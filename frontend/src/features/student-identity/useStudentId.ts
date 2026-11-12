import { createContext, createElement, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { useLocation, useNavigate } from "react-router";
import type { Student } from "../../api/apiTypes";
import { useMe, useMyStudents } from "../account/useAccount";

const STORAGE_KEY = "bhorti-desk.selected-student";

interface StudentIdContextValue {
  // The birth registration number of the student this guardian is acting for.
  studentId: string | null;
  students: Student[];
  selectStudent: (birthRegistrationNumber: string) => void;
  // Apply, Pay and Cancel call this when nobody is selected: it sends the person to
  // sign in, or to add a student, and comes back here afterwards.
  askForNumber: () => void;
}

const StudentIdContext = createContext<StudentIdContextValue | null>(null);

function readStored(): string | null {
  try {
    return localStorage.getItem(STORAGE_KEY);
  } catch {
    return null;
  }
}

function writeStored(value: string) {
  try {
    localStorage.setItem(STORAGE_KEY, value);
  } catch {
    // Storage is blocked: the choice lasts for this visit only.
  }
}

export function StudentIdProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient();
  const navigate = useNavigate();
  const location = useLocation();
  const me = useMe();
  const isGuardian = me.data?.kind === "GUARDIAN";
  const studentsQuery = useMyStudents(isGuardian);
  const students = useMemo(() => (isGuardian ? studentsQuery.data ?? [] : []), [isGuardian, studentsQuery.data]);
  const [chosen, setChosen] = useState<string | null>(readStored);

  // Only a student that is really on this account can be the selected one.
  const studentId = students.find((s) => s.birthRegistrationNumber === chosen)?.birthRegistrationNumber
    ?? students[0]?.birthRegistrationNumber
    ?? null;

  useEffect(() => {
    if (studentId && studentId !== chosen) writeStored(studentId);
  }, [studentId, chosen]);

  const selectStudent = useCallback(
    (birthRegistrationNumber: string) => {
      if (birthRegistrationNumber !== studentId) {
        // Nothing cached for one child may show for another.
        queryClient.removeQueries({ queryKey: ["student"] });
      }
      setChosen(birthRegistrationNumber);
      writeStored(birthRegistrationNumber);
    },
    [queryClient, studentId],
  );

  const askForNumber = useCallback(() => {
    const back = { state: { from: location.pathname } };
    if (!isGuardian) navigate("/login", back);
    else navigate("/students", back);
  }, [isGuardian, navigate, location.pathname]);

  const value = useMemo<StudentIdContextValue>(
    () => ({ studentId, students, selectStudent, askForNumber }),
    [studentId, students, selectStudent, askForNumber],
  );

  return createElement(StudentIdContext.Provider, { value }, children);
}

export function useStudentId(): StudentIdContextValue {
  const context = useContext(StudentIdContext);
  if (!context) throw new Error("useStudentId must be used inside StudentIdProvider");
  return context;
}
