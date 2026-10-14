import { api, unwrap } from "./client";
import type { components } from "./schema";

export type RevenueReport = components["schemas"]["RevenueReport"];
export type RevenueByMethod = components["schemas"]["RevenueByMethod"];
export type OutstandingReport = components["schemas"]["OutstandingReport"];
export type OutstandingByPatient = components["schemas"]["OutstandingByPatient"];
export type VisitReport = components["schemas"]["VisitReport"];
export type VisitsByDoctor = components["schemas"]["VisitsByDoctor"];

export interface ReportRange {
  fromUtc?: string;
  toUtc?: string;
}

export function getRevenueReport(range: ReportRange = {}): Promise<RevenueReport> {
  return api.GET("/api/reports/revenue", { params: { query: range } }).then(unwrap);
}

export function getOutstandingReport(range: ReportRange = {}): Promise<OutstandingReport> {
  return api.GET("/api/reports/outstanding", { params: { query: range } }).then(unwrap);
}

export function getVisitReport(range: ReportRange = {}): Promise<VisitReport> {
  return api.GET("/api/reports/visits", { params: { query: range } }).then(unwrap);
}