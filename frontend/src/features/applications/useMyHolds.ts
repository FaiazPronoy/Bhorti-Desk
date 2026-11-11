import { useQuery } from "@tanstack/react-query";
import { admissionApi } from "../../api/admissionApiClient";
import { useStudentId } from "../student-identity/useStudentId";

// Everything cached for a student lives under ["student", ...], so changing the number
// can throw it all away in one call.
export const myHoldsKey = (studentId: string | null) => ["student", studentId, "holds"] as const;

export function useMyHolds() {
  const { studentId } = useStudentId();
  return useQuery({
    queryKey: myHoldsKey(studentId),
    queryFn: () => admissionApi.myHolds(studentId!),
    enabled: studentId !== null,
    refetchInterval: 10_000,
  });
}
