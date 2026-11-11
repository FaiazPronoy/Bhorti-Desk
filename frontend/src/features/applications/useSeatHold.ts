import { useCallback } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { admissionApi } from "../../api/admissionApiClient";
import { classListKey } from "../classes/useClassList";

export const holdKey = (holdId: string) => ["hold", holdId] as const;

export function useSeatHold(holdId: string) {
  return useQuery({
    queryKey: holdKey(holdId),
    queryFn: () => admissionApi.getHold(holdId),
    refetchInterval: 5000,
    refetchIntervalInBackground: true,
  });
}

// After apply, pay, cancel or an expiry, the seat numbers and "my applications" change
// for everyone, so both are read again straight away.
export function useRefreshSeats() {
  const queryClient = useQueryClient();
  return useCallback(() => {
    void queryClient.invalidateQueries({ queryKey: classListKey });
    void queryClient.invalidateQueries({ queryKey: ["student"] });
  }, [queryClient]);
}

export function useApplyForSeat() {
  const refresh = useRefreshSeats();
  return useMutation({
    mutationFn: ({ classId, studentId }: { classId: string; studentId: string }) =>
      admissionApi.applyForSeat(classId, studentId),
    onSettled: refresh,
  });
}

export function usePayFee(holdId: string) {
  const queryClient = useQueryClient();
  const refresh = useRefreshSeats();
  return useMutation({
    mutationFn: (studentId: string) => admissionApi.payFee(holdId, studentId),
    onSettled: () => {
      void queryClient.invalidateQueries({ queryKey: holdKey(holdId) });
      refresh();
    },
  });
}

export function useCancelHold(holdId: string) {
  const queryClient = useQueryClient();
  const refresh = useRefreshSeats();
  return useMutation({
    mutationFn: (studentId: string) => admissionApi.cancelHold(holdId, studentId),
    onSettled: () => {
      void queryClient.invalidateQueries({ queryKey: holdKey(holdId) });
      refresh();
    },
  });
}
