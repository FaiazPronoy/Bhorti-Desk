import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useCallback } from "react";
import { admissionApi } from "../../api/admissionApiClient";
import { isApiError } from "../../api/apiErrors";
import type { Me } from "../../api/apiTypes";

export const meKey = ["account", "me"] as const;
export const myStudentsKey = ["account", "students"] as const;

// Who is signed in, or null. A 401 is an answer here, not an error.
export function useMe() {
  return useQuery({
    queryKey: meKey,
    queryFn: async (): Promise<Me | null> => {
      try {
        return await admissionApi.me();
      } catch (error) {
        if (isApiError(error) && error.status === 401) return null;
        throw error;
      }
    },
    staleTime: 60_000,
  });
}

export function useMyStudents(enabled: boolean) {
  return useQuery({ queryKey: myStudentsKey, queryFn: admissionApi.myStudents, enabled });
}

// After signing in or out, everything cached for the old person is thrown away.
export function useResetAccount() {
  const queryClient = useQueryClient();
  return useCallback(async () => {
    queryClient.removeQueries({ queryKey: ["student"] });
    queryClient.removeQueries({ queryKey: myStudentsKey });
    await queryClient.invalidateQueries({ queryKey: meKey });
  }, [queryClient]);
}
