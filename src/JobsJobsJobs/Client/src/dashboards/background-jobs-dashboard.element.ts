import { css, customElement, html, state } from "@umbraco-cms/backoffice/external/lit";
import { UMB_AUTH_CONTEXT } from "@umbraco-cms/backoffice/auth";
import type { UUIButtonState } from "@umbraco-cms/backoffice/external/uui";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";

interface BackgroundJobRunLogEntry {
  loggedAt: string;
  level: string;
  message: string;
}

interface BackgroundJobLatestRun {
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

interface BackgroundJobDashboardItem {
  alias: string;
  name: string;
  type: string;
  period: string;
  delay: string;
  serverRoles: Array<string>;
  allowManualTrigger: boolean;
  isRunning: boolean;
  lastStartedAt?: string;
  lastCompletedAt?: string;
  lastDuration?: string;
  lastSucceededAt?: string;
  lastFailedAt?: string;
  lastStatus: string;
  lastError?: string;
  lastMessage?: string;
  latestRun?: BackgroundJobLatestRun;
}

interface BackgroundJobDashboardCollectionResponseModel {
  total: number;
  items: Array<BackgroundJobDashboardItem>;
}

type BackgroundJobFilter = "all" | "running" | "failed" | "succeeded" | "idle";

@customElement("jobs-jobs-jobs-background-jobs-dashboard")
export class JobsJobsJobsBackgroundJobsDashboardElement extends UmbLitElement {
  private static readonly _autoRefreshIntervalMs = 5000;

  private _authToken?: string | (() => Promise<string>);

  private _authCredentials: RequestCredentials = "include";

  private _autoRefreshHandle?: number;

  @state()
  private _items: Array<BackgroundJobDashboardItem> = [];

  @state()
  private _isLoading = false;

  @state()
  private _reloadState: UUIButtonState = undefined;

  @state()
  private _runStates: Record<string, UUIButtonState> = {};

  @state()
  private _errorMessage = "";

  @state()
  private _statusFilter: BackgroundJobFilter = "all";

  constructor() {
    super();

    this.consumeContext(UMB_AUTH_CONTEXT, (authContext) => {
      const config = authContext?.getOpenApiConfiguration();
      this._authToken = config?.token;
      this._authCredentials = config?.credentials ?? "include";
      this._load();
    });
  }

  override connectedCallback() {
    super.connectedCallback();
    this._startAutoRefresh();
  }

  override disconnectedCallback() {
    this._stopAutoRefresh();
    super.disconnectedCallback();
  }

  private async _getAuthToken(): Promise<string | undefined> {
    if (typeof this._authToken === "function") {
      return this._authToken();
    }

    return this._authToken;
  }

  private async _load() {
    this._isLoading = true;
    this._errorMessage = "";

    try {
      const response = await this._fetch("/umbraco/jobsjobsjobs/api/v1/background-jobs", {
        method: "GET",
      });

      if (!response.ok) {
        throw new Error(await this._readProblem(response));
      }

      const data = (await response.json()) as BackgroundJobDashboardCollectionResponseModel;
      this._items = data.items ?? [];
    } catch (error) {
      this._errorMessage = error instanceof Error ? error.message : "Could not load background jobs.";
    } finally {
      this._isLoading = false;
    }
  }

  private async _reload() {
    this._reloadState = "waiting";
    await this._load();
    this._reloadState = this._errorMessage ? "failed" : "success";
  }

  private _startAutoRefresh() {
    this._stopAutoRefresh();
    this._autoRefreshHandle = window.setInterval(() => {
      void this._autoRefresh();
    }, JobsJobsJobsBackgroundJobsDashboardElement._autoRefreshIntervalMs);
  }

  private _stopAutoRefresh() {
    if (this._autoRefreshHandle !== undefined) {
      window.clearInterval(this._autoRefreshHandle);
      this._autoRefreshHandle = undefined;
    }
  }

  private async _autoRefresh() {
    if (this._isLoading) {
      return;
    }

    await this._load();
  }

  private async _runJob(alias: string) {
    this._runStates = { ...this._runStates, [alias]: "waiting" };
    this._errorMessage = "";

    try {
      const response = await this._fetch(`/umbraco/jobsjobsjobs/api/v1/background-jobs/run/${encodeURIComponent(alias)}`, {
        method: "POST",
      });

      if (!response.ok) {
        throw new Error(await this._readProblem(response));
      }

      this._runStates = { ...this._runStates, [alias]: "success" };
      await this._load();
    } catch (error) {
      this._runStates = { ...this._runStates, [alias]: "failed" };
      this._errorMessage = error instanceof Error ? error.message : `Could not run ${alias}.`;
    }
  }

  private async _fetch(input: RequestInfo | URL, init?: RequestInit) {
    const headers = new Headers(init?.headers);
    headers.set("Content-Type", "application/json");

    const authToken = await this._getAuthToken();

    if (authToken) {
      headers.set("Authorization", `Bearer ${authToken}`);
    }

    return fetch(input, {
      ...init,
      credentials: this._authCredentials,
      headers,
    });
  }

  private async _readProblem(response: Response) {
    try {
      const problem = (await response.json()) as { detail?: string; title?: string; message?: string };
      return problem.detail ?? problem.title ?? problem.message ?? `Request failed with status ${response.status}.`;
    } catch {
      return `Request failed with status ${response.status}.`;
    }
  }

  private _formatDate(value?: string) {
    if (!value) return "-";
    const date = new Date(value);
    return Number.isNaN(date.getTime()) ? value : date.toLocaleString();
  }

  private _formatTimeSpan(value: string) {
    return value.startsWith("00:") || value.startsWith("0.") ? value : value;
  }

  private _formatDuration(value?: string) {
    if (!value) return "-";

    const match = /^(?:(\d+)\.)?(\d{2}):(\d{2}):(\d{2})(?:\.(\d+))?$/.exec(value);
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
      const parts = [];
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

  private _getStatusLabel(item: BackgroundJobDashboardItem) {
    return item.isRunning ? "Running" : item.lastStatus;
  }

  private _getStatusClassFromValue(status: string) {
    return `status-badge status-${status.toLowerCase()}`;
  }

  private _getStatusClass(item: BackgroundJobDashboardItem) {
    return this._getStatusClassFromValue(this._getStatusLabel(item));
  }

  private _matchesFilter(item: BackgroundJobDashboardItem) {
    switch (this._statusFilter) {
      case "running":
        return item.isRunning;
      case "failed":
        return item.lastStatus === "Failed";
      case "succeeded":
        return item.lastStatus === "Succeeded";
      case "idle":
        return item.lastStatus === "Idle" && item.isRunning === false;
      default:
        return true;
    }
  }

  private _getVisibleItems() {
    return this._items.filter((item) => this._matchesFilter(item));
  }

  private _onFilterChange(event: Event) {
    this._statusFilter = (event.target as HTMLSelectElement).value as BackgroundJobFilter;
  }

  private _renderLatestRun(item: BackgroundJobDashboardItem) {
    const run = item.latestRun;

    if (!run) {
      return "";
    }

    return html`
      <div class="persisted-run">
        <div class="persisted-run-heading">
          <strong>Latest stored run</strong>
          <span class=${this._getStatusClassFromValue(run.status)}>${run.status}</span>
        </div>
        <div class="persisted-run-summary">
          <div class="persisted-run-field">
            <span class="muted">Started</span>
            <span>${this._formatDate(run.startedAt)}</span>
          </div>
          <div class="persisted-run-field">
            <span class="muted">Completed</span>
            <span>${this._formatDate(run.completedAt)}</span>
          </div>
          <div class="persisted-run-field">
            <span class="muted">Trigger</span>
            <span>${run.trigger}</span>
          </div>
          <div class="persisted-run-field">
            <span class="muted">Duration</span>
            <span>${this._formatDuration(run.duration)}</span>
          </div>
        </div>
        ${run.error
          ? html`<div><strong>Stored error:</strong> ${run.error}</div>`
          : run.message
            ? html`<div><strong>Stored message:</strong> ${run.message}</div>`
            : ""}
        ${run.logs.length > 0
          ? html`
              <div class="persisted-run-logs">
                <strong>Stored logs</strong>
                <ul class="log-list">
                  ${run.logs.map(
                    (log) => html`
                      <li class="log-item">
                        <div class="log-meta">
                          <span class="log-time">${this._formatDate(log.loggedAt)}</span>
                          <span class=${this._getStatusClassFromValue(log.level)}>${log.level}</span>
                        </div>
                        <span class="log-message">${log.message}</span>
                      </li>
                    `,
                  )}
                </ul>
              </div>
            `
          : html`<div class="muted">No stored logs for the latest run.</div>`}
      </div>
    `;
  }

  private _renderRows() {
    const items = this._getVisibleItems();

    if (items.length === 0) {
      const message = this._items.length === 0
        ? "No recurring background jobs found."
        : "No jobs match the selected filter.";

      return html`<tr><td colspan="8">${this._isLoading ? "Loading jobs…" : message}</td></tr>`;
    }

    return items.map(
      (item) => html`
        <tr>
          <td class="job-cell">
            <strong>${item.name}</strong>
            <div class="muted job-meta" title=${item.type}>${item.type}</div>
          </td>
          <td><span class=${this._getStatusClass(item)}>${this._getStatusLabel(item)}</span></td>
          <td>${this._formatDate(item.lastSucceededAt)}</td>
          <td>${this._formatDate(item.lastFailedAt)}</td>
          <td>${this._formatDate(item.lastStartedAt)}</td>
          <td>${this._formatDuration(item.lastDuration)}</td>
          <td>${this._formatTimeSpan(item.period)}</td>
          <td>
            <uui-button
              look="primary"
              label="Run now"
              ?disabled=${item.allowManualTrigger === false || item.isRunning}
              .state=${this._runStates[item.alias]}
              @click=${() => this._runJob(item.alias)}>
              Run now
            </uui-button>
          </td>
        </tr>
        ${item.lastError
          ? html`<tr class="details"><td colspan="8"><strong>Error:</strong> ${item.lastError}${item.lastMessage ? html`<div><strong>Message:</strong> ${item.lastMessage}</div>` : ""}${this._renderLatestRun(item)}</td></tr>`
          : item.lastMessage
            ? html`<tr class="details"><td colspan="8"><strong>Message:</strong> ${item.lastMessage}${this._renderLatestRun(item)}</td></tr>`
            : item.latestRun
              ? html`<tr class="details"><td colspan="8">${this._renderLatestRun(item)}</td></tr>`
              : ""}`,
    );
  }

  override render() {
    return html`
      <uui-box headline="Background Jobs">
        <uui-button slot="header-actions" look="secondary" label="Refresh" @click=${this._reload} .state=${this._reloadState}>
          Refresh
        </uui-button>
        <p>Recurring background jobs registered in Umbraco with status and manual trigger.</p>
        <p class="muted refresh-info">Auto-refreshes every ${JobsJobsJobsBackgroundJobsDashboardElement._autoRefreshIntervalMs / 1000} seconds.</p>
        ${this._errorMessage ? html`<p class="error">${this._errorMessage}</p>` : ""}
        <div class="toolbar">
          <label class="filter-label" for="status-filter">Status filter</label>
          <select id="status-filter" class="filter-select" @change=${this._onFilterChange} .value=${this._statusFilter}>
            <option value="all">All</option>
            <option value="running">Running</option>
            <option value="failed">Failed</option>
            <option value="succeeded">Succeeded</option>
            <option value="idle">Idle</option>
          </select>
        </div>
        <div class="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Job</th>
                <th>Status</th>
                <th>Last success</th>
                <th>Last failure</th>
                <th>Last start</th>
                <th>Last duration</th>
                <th>Period</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              ${this._renderRows()}
            </tbody>
          </table>
        </div>
      </uui-box>
    `;
  }

  static override styles = [
    UmbTextStyles,
    css`
      :host {
        display: block;
        padding: var(--uui-size-layout-1);
      }

      .table-wrap {
        overflow: auto;
      }

      table {
        width: 100%;
        border-collapse: collapse;
        table-layout: fixed;
      }

      th,
      td {
        padding: var(--uui-size-space-3);
        border-bottom: 1px solid var(--uui-color-border);
        text-align: left;
        vertical-align: top;
      }

      .muted {
        color: var(--uui-color-text-alt);
        font-size: var(--uui-type-small-size);
      }

      .refresh-info {
        margin-top: calc(var(--uui-size-space-1) * -1);
      }

      .toolbar {
        display: flex;
        align-items: center;
        gap: var(--uui-size-space-3);
        margin-bottom: var(--uui-size-space-4);
      }

      .filter-label {
        font-size: var(--uui-type-small-size);
        color: var(--uui-color-text-alt);
      }

      .filter-select {
        min-width: 10rem;
        padding: var(--uui-size-space-2) var(--uui-size-space-3);
        border: 1px solid var(--uui-color-border);
        border-radius: var(--uui-border-radius);
        background: var(--uui-color-surface);
        color: var(--uui-color-text);
      }

      .job-cell {
        width: 30%;
        min-width: 16rem;
      }

      .job-meta {
        display: block;
        max-width: 24rem;
        overflow: hidden;
        text-overflow: ellipsis;
        white-space: nowrap;
      }

      .status-badge {
        display: inline-flex;
        align-items: center;
        padding: 0.2rem 0.55rem;
        border-radius: 999px;
        font-size: var(--uui-type-small-size);
        font-weight: 700;
        white-space: nowrap;
      }

      .status-running {
        background: color-mix(in srgb, var(--uui-color-warning) 18%, white);
        color: var(--uui-color-warning-emphasis);
      }

      .status-succeeded {
        background: color-mix(in srgb, var(--uui-color-positive) 18%, white);
        color: var(--uui-color-positive-emphasis);
      }

      .status-failed {
        background: color-mix(in srgb, var(--uui-color-danger) 18%, white);
        color: var(--uui-color-danger-emphasis);
      }

      .status-idle,
      .status-ignored {
        background: var(--uui-color-surface-alt);
        color: var(--uui-color-text);
      }

      .details td {
        background: var(--uui-color-surface-alt);
      }

      .persisted-run {
        margin-top: var(--uui-size-space-3);
        display: grid;
        gap: var(--uui-size-space-2);
      }

      .persisted-run-heading {
        display: flex;
        flex-wrap: wrap;
        align-items: center;
        gap: var(--uui-size-space-3);
      }

      .persisted-run-summary {
        display: grid;
        grid-template-columns: repeat(auto-fit, minmax(10rem, 1fr));
        gap: var(--uui-size-space-3);
      }

      .persisted-run-field {
        display: grid;
        gap: var(--uui-size-space-1);
        padding: var(--uui-size-space-2) var(--uui-size-space-3);
        background: var(--uui-color-surface);
        border-radius: var(--uui-border-radius);
      }

      .persisted-run-logs {
        display: grid;
        gap: var(--uui-size-space-2);
      }

      .log-list {
        list-style: none;
        margin: 0;
        padding: 0;
        display: grid;
        gap: var(--uui-size-space-2);
      }

      .log-item {
        display: grid;
        gap: var(--uui-size-space-1);
        padding: var(--uui-size-space-2) var(--uui-size-space-3);
        background: var(--uui-color-surface);
        border-radius: var(--uui-border-radius);
      }

      .log-meta {
        display: flex;
        flex-wrap: wrap;
        align-items: center;
        gap: var(--uui-size-space-2);
      }

      .log-time,
      .log-level {
        font-size: var(--uui-type-small-size);
        color: var(--uui-color-text-alt);
      }

      .status-information {
        background: var(--uui-color-surface-alt);
        color: var(--uui-color-text);
      }

      .status-warning {
        background: color-mix(in srgb, var(--uui-color-warning) 18%, white);
        color: var(--uui-color-warning-emphasis);
      }

      .status-error {
        background: color-mix(in srgb, var(--uui-color-danger) 18%, white);
        color: var(--uui-color-danger-emphasis);
      }

      .log-message {
        white-space: pre-wrap;
        word-break: break-word;
      }

      .error {
        color: var(--uui-color-danger);
        font-weight: 700;
      }
    `,
  ];
}

export default JobsJobsJobsBackgroundJobsDashboardElement;

declare global {
  interface HTMLElementTagNameMap {
    "jobs-jobs-jobs-background-jobs-dashboard": JobsJobsJobsBackgroundJobsDashboardElement;
  }
}
