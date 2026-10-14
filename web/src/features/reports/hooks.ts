import { useQuery } from "@tanstack/react-query";
import {
  getOutstandingReport,
  getRevenueReport,
  getVisitReport,
  type ReportRange,
} from "../../lib/api/reports";

export function useRevenueReport(range: ReportRange) {
  return useQuery({
    queryKey: ["reports", "revenue", range],
    queryFn: () => getRevenueReport(range),
  });
}

export function useOutstandingReport(range: ReportRange) {
  return useQuery({
    queryKey: ["reports", "outstanding", range],
    queryFn: () => getOutstandingReport(range),
  });
}

export function useVisitReport(range: ReportRange) {
  return useQuery({
    queryKey: ["reports", "visits", range],
    queryFn: () => getVisitReport(range),
  });
}