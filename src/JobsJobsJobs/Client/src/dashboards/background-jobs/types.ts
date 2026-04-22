export interface BackgroundJobRunLogEntry {
  loggedAt: string;
  level: string;
  message: string;
}

export interface BackgroundJobDashboardRun {
  id: string;
  trigger: string;
  status: string;
  startedAt: string;
  completedAt?: string;
  duration?: string;
  message?: string;
  error?: string;
  logs: Array<BackgroundJobRunLogEntry>;
}

export interface BackgroundJobDashboardItem {
  alias: string;
  name: string;
  type: string;
  period: string;
  delay: string;
  usesCronSchedule: boolean;
  scheduleDisplay?: string;
  cronExpression?: string;
  timeZoneId?: string;
  serverRoles: Array<string>;
  allowManualTrigger: boolean;
  canStop: boolean;
  isRunning: boolean;
  stopRequested: boolean;
  lastStartedAt?: string;
  lastCompletedAt?: string;
  lastDuration?: string;
  lastSucceededAt?: string;
  lastFailedAt?: string;
  lastStatus: string;
  lastError?: string;
  lastMessage?: string;
  latestRun?: BackgroundJobDashboardRun;
  recentRuns: Array<BackgroundJobDashboardRun>;
}

export interface BackgroundJobDashboardCollectionResponseModel {
  total: number;
  items: Array<BackgroundJobDashboardItem>;
}

export interface BackgroundJobRunLogsResponseModel {
  runId: string;
  total: number;
  logs: Array<BackgroundJobRunLogEntry>;
}

export type BackgroundJobFilter = "all" | "running" | "failed" | "succeeded" | "idle";

export type BackgroundJobRunStatus = "Idle" | "Running" | "Succeeded" | "Failed" | "Stopped" | "Ignored";

export type BackgroundJobRunTriggerFilter = "Automatic" | "Manual";

export interface BackgroundJobRunHistoryQuery {
  jobAlias?: string;
  statuses?: Array<BackgroundJobRunStatus>;
  trigger?: BackgroundJobRunTriggerFilter;
  startedAfter?: string;
  startedBefore?: string;
  search?: string;
  page: number;
  pageSize: number;
}

export interface BackgroundJobRunHistoryItem {
  id: string;
  jobAlias: string;
  jobName: string;
  trigger: string;
  status: string;
  startedAt: string;
  completedAt?: string;
  duration?: string;
  message?: string;
  error?: string;
}

export interface BackgroundJobRunHistoryPageResponseModel {
  page: number;
  pageSize: number;
  total: number;
  items: Array<BackgroundJobRunHistoryItem>;
}
