import { createContext, createElement, useCallback, useContext, useMemo, useRef, useState, type ReactNode, type RefObject } from "react";
import { useQueryClient } from "@tanstack/react-query";

const STORAGE_KEY = "bhorti-desk.student-id";

interface StudentIdContextValue {
  studentId: string | null;
  saveStudentId: (digits: string) => void;
  clearStudentId: () => void;
  // Apply, Pay and Cancel call this when no valid number is entered. It moves focus to
  // the number box and asks the box to say why.
  askForNumber: () => void;
  numberWasAskedFor: boolean;
  inputRef: RefObject<HTMLInputElement | null>;
}

const StudentIdContext = createContext<StudentIdContextValue | null>(null);

function readStored(): string | null {
  try {
    const stored = localStorage.getItem(STORAGE_KEY);
    return stored && /^[0-9]{17}$/.test(stored) ? stored : null;
  } catch {
    return null;
  }
}

function writeStored(value: string | null) {
  try {
    if (value) localStorage.setItem(STORAGE_KEY, value);
    else localStorage.removeItem(STORAGE_KEY);
  } catch {
    // Storage is blocked: keep the number in memory for this visit only.
  }
}

export function StudentIdProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient();
  const [studentId, setStudentId] = useState<string | null>(readStored);
  const [numberWasAskedFor, setNumberWasAskedFor] = useState(false);
  const inputRef = useRef<HTMLInputElement | null>(null);

  const switchStudent = useCallback(
    (next: string | null) => {
      if (next !== studentId) {
        // Nothing cached for the previous student may show for the next one.
        queryClient.removeQueries({ queryKey: ["student"] });
      }
      setStudentId(next);
      writeStored(next);
    },
    [queryClient, studentId],
  );

  const value = useMemo<StudentIdContextValue>(
    () => ({
      studentId,
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

export function useStudentId(): StudentIdContextValue {
  const context = useContext(StudentIdContext);
  if (!context) throw new Error("useStudentId must be used inside StudentIdProvider");
  return context;
}
