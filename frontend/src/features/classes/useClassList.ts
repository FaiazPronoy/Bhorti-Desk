import { useQuery } from "@tanstack/react-query";
import { admissionApi } from "../../api/admissionApiClient";

export const classListKey = ["classes"] as const;

// Every 4 seconds, even in a background tab, so two tabs side by side both stay live.
export function useClassList() {
  return useQuery({
    queryKey: classListKey,
    queryFn: admissionApi.listClasses,
    refetchInterval: 4000,
    refetchIntervalInBackground: true,
  });
}
