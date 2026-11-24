import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { admissionApi } from "../../api/admissionApiClient";
import { useStudentId } from "../student-identity/useStudentId";

export const waitlistKey = (studentId: string | null) => ["student", studentId, "waitlist"] as const;
export const noticesKey = (studentId: string | null) => ["student", studentId, "notices"] as const;

export function useWaitlist() {
  const { studentId } = useStudentId();
  return useQuery({
    queryKey: waitlistKey(studentId),
    queryFn: () => admissionApi.myWaitlist(studentId!),
    enabled: studentId !== null,
    refetchInterval: 10_000,
    retry: false,
  });
}

export function useNotices() {
  const { studentId } = useStudentId();
  return useQuery({
    queryKey: noticesKey(studentId),
    queryFn: () => admissionApi.myNotices(studentId!),
    enabled: studentId !== null,
    refetchInterval: 15_000,
    retry: false,
  });
}

export function useJoinWaitlist() {
  const queryClient = useQueryClient();
  const { studentId } = useStudentId();
  return useMutation({
    mutationFn: (classId: string) => admissionApi.joinWaitlist(classId, studentId!),
    onSettled: () => queryClient.invalidateQueries({ queryKey: waitlistKey(studentId) }),
  });
}
