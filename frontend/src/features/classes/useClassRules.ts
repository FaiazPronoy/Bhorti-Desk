import { useQuery } from "@tanstack/react-query";
import { admissionApi } from "../../api/admissionApiClient";

export const classRulesKey = ["class-rules"] as const;

export function useClassRules() {
  return useQuery({ queryKey: classRulesKey, queryFn: admissionApi.classRules, staleTime: 30_000, retry: false });
}
