import type { BackgroundJobDashboardItem } from "./types.js";

const DATE_TIME_FORMATTER = new Intl.DateTimeFormat(undefined, {
  dateStyle: "medium",
  timeStyle: "short",
});

const DURATION_PATTERN = /^(?:(\d+)\.)?(\d{2}):(\d{2}):(\d{2})(?:\.(\d+))?$/;

export function getViewerTimeZone(): string {
  return Intl.DateTimeFormat().resolvedOptions().timeZone || "your local time";
}

export function formatDate(value?: string): string {
  if (!value) return "-";
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? value : DATE_TIME_FORMATTER.format(date);
}

export function formatDuration(value?: string): string {
  if (!value) return "-";

  const match = DURATION_PATTERN.exec(value);
  if (!match) {
    return value;
  }

  const days = Number(match[1] ?? "0");
  const hours = Number(match[2]);
  const minutes = Number(match[3]);
  const seconds = Number(match[4]);
  const fractional = match[5] ?? "";
  const milliseconds = fractional ? Math.round(Number(`0.${fractional}`) * 1000) : 0;

  if (days > 0 || hours > 0 || minutes > 0) {
    const parts: Array<string> = [];
    if (days > 0) parts.push(`${days}d`);
    if (hours > 0) parts.push(`${hours}h`);
    if (minutes > 0) parts.push(`${minutes}m`);
    if (seconds > 0) parts.push(`${seconds}s`);
    return parts.join(" ");
  }

  if (seconds > 0) {
    if (milliseconds === 0) {
      return `${seconds}s`;
    }

    return `${seconds}.${milliseconds.toString().padStart(3, "0").replace(/0+$/, "")}s`;
  }

  return `${milliseconds}ms`;
}

export function formatTimeSpan(value: string): string {
  return value;
}

export function getStatusClassFromValue(status: string): string {
  return `status-badge status-${status.toLowerCase()}`;
}

export function getStatusLabel(item: BackgroundJobDashboardItem): string {
  if (item.stopRequested) {
    return "StopRequested";
  }

  return item.isRunning ? "Running" : item.lastStatus;
}

export function getStatusClass(item: BackgroundJobDashboardItem): string {
  return getStatusClassFromValue(getStatusLabel(item));
}

export function normalizeText(value?: string): string | undefined {
  const trimmed = value?.trim();
  return trimmed ? trimmed : undefined;
}

export function isSameText(left?: string, right?: string): boolean {
  return normalizeText(left) === normalizeText(right);
}

export function getJobCardClass(item: BackgroundJobDashboardItem): string {
  if (item.isRunning) {
    return "job-card-running";
  }

  if (item.lastStatus === "Failed") {
    return "job-card-failed";
  }

  if (item.lastStatus === "Succeeded") {
    return "job-card-succeeded";
  }

  if (!item.lastStartedAt && !item.latestRun) {
    return "job-card-never-run";
  }

  return "";
}
