import { createContext, createElement, useCallback, useContext, useEffect, useMemo, useRef, useState, type ReactNode, type RefObject } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { useLocation, useNavigate } from "react-router";
import type { Student } from "../../api/apiTypes";
import { signInRequired } from "../../lib/runtimeConfig";
import { useMe, useMyStudents } from "../account/useAccount";

const SELECTED_STUDENT_KEY = "bhorti-desk.selected-student";
const TYPED_NUMBER_KEY = "bhorti-desk.student-id";

interface StudentIdContextValue {
  // The birth registration number of the student this person is acting for.
  studentId: string | null;
  students: Student[];
  selectStudent: (birthRegistrationNumber: string) => void;
  // Apply, Pay and Cancel call this when nobody is chosen. Without sign-in it moves focus
  // to the number box. With sign-in it goes to the sign-in or students page.
  askForNumber: () => void;
  // Used by the number box when sign-in is off.
  saveStudentId: (digits: string) => void;
  clearStudentId: () => void;
  numberWasAskedFor: boolean;
  inputRef: RefObject<HTMLInputElement | null>;
}

const StudentIdContext = createContext<StudentIdContextValue | null>(null);

function readStored(key: string): string | null {
  try {
    const stored = localStorage.getItem(key);
    return stored && /^[0-9]{17}$/.test(stored) ? stored : null;
  } catch {
    return null;
  }
}

function writeStored(key: string, value: string | null) {
  try {
    if (value) localStorage.setItem(key, value);
    else localStorage.removeItem(key);
  } catch {
    // Storage is blocked: the choice lasts for this visit only.
  }
}

export function StudentIdProvider({ children }: { children: ReactNode }) {
  return signInRequired()
    ? createElement(AccountStudentProvider, null, children)
    : createElement(TypedNumberProvider, null, children);
}

// Default mode: the number typed in the box is the identity.
function TypedNumberProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient();
  const [studentId, setStudentId] = useState<string | null>(() => readStored(TYPED_NUMBER_KEY));
  const [numberWasAskedFor, setNumberWasAskedFor] = useState(false);
  const inputRef = useRef<HTMLInputElement | null>(null);

  const switchStudent = useCallback(
    (next: string | null) => {
      if (next !== studentId) {
        // Nothing cached for the previous student may show for the next one.
        queryClient.removeQueries({ queryKey: ["student"] });
      }
      setStudentId(next);
      writeStored(TYPED_NUMBER_KEY, next);
    },
    [queryClient, studentId],
  );

  const value = useMemo<StudentIdContextValue>(
    () => ({
      studentId,
      students: [],
      selectStudent: () => {},
      saveStudentId: (digits) => {
        setNumberWasAskedFor(false);
        switchStudent(digits);
      },
      clearStudentId: () => {
        switchStudent(null);
        inputRef.current?.focus();
      },
      askForNumber: () => {
        setNumberWasAskedFor(true);
        inputRef.current?.focus();
        inputRef.current?.scrollIntoView({ block: "center" });
      },
      numberWasAskedFor,
      inputRef,
    }),
    [studentId, numberWasAskedFor, switchStudent],
  );

  return createElement(StudentIdContext.Provider, { value }, children);
}

// Sign-in mode: the guardian picks one of the students on the account.
function AccountStudentProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient();
  const navigate = useNavigate();
  const location = useLocation();
  const me = useMe();
  const isGuardian = me.data?.kind === "GUARDIAN";
  const studentsQuery = useMyStudents(isGuardian);
  const students = useMemo(() => (isGuardian ? studentsQuery.data ?? [] : []), [isGuardian, studentsQuery.data]);
  const [chosen, setChosen] = useState<string | null>(() => readStored(SELECTED_STUDENT_KEY));
  const inputRef = useRef<HTMLInputElement | null>(null);

  // Only a student that is really on this account can be the selected one.
  const studentId = students.find((s) => s.birthRegistrationNumber === chosen)?.birthRegistrationNumber
    ?? students[0]?.birthRegistrationNumber
    ?? null;

  useEffect(() => {
    if (studentId && studentId !== chosen) writeStored(SELECTED_STUDENT_KEY, studentId);
  }, [studentId, chosen]);

  const selectStudent = useCallback(
    (birthRegistrationNumber: string) => {
      if (birthRegistrationNumber !== studentId) {
        // Nothing cached for one child may show for another.
        queryClient.removeQueries({ queryKey: ["student"] });
      }
      setChosen(birthRegistrationNumber);
      writeStored(SELECTED_STUDENT_KEY, birthRegistrationNumber);
    },
    [queryClient, studentId],
  );

  const askForNumber = useCallback(() => {
    const back = { state: { from: location.pathname } };
    if (!isGuardian) navigate("/login", back);
    else navigate("/students", back);
  }, [isGuardian, navigate, location.pathname]);

  const value = useMemo<StudentIdContextValue>(
    () => ({
      studentId,
      students,
      selectStudent,
      askForNumber,
      saveStudentId: selectStudent,
      clearStudentId: () => {},
      numberWasAskedFor: false,
      inputRef,
    }),
    [studentId, students, selectStudent, askForNumber],
  );

  return createElement(StudentIdContext.Provider, { value }, children);
}

export function useStudentId(): StudentIdContextValue {
  const context = useContext(StudentIdContext);
  if (!context) throw new Error("useStudentId must be used inside StudentIdProvider");
  return context;
}
