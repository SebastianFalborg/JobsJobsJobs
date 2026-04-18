import { customElement, html, state } from "@umbraco-cms/backoffice/external/lit";
import { UMB_AUTH_CONTEXT } from "@umbraco-cms/backoffice/auth";
import type { UmbAuthContext } from "@umbraco-cms/backoffice/auth";
import type { UUIButtonState } from "@umbraco-cms/backoffice/external/uui";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";
import { BackgroundJobsApi } from "./background-jobs/api.js";
import {
  formatDate,
  formatDuration,
  formatTimeSpan,
  getJobCardClass,
  getStatusClass,
  getStatusClassFromValue,
  getStatusLabel,
  getViewerTimeZone,
  isSameText,
  normalizeText,
} from "./background-jobs/formatting.js";
import { backgroundJobsDashboardStyles } from "./background-jobs/styles.js";
import type {
  BackgroundJobDashboardItem,
  BackgroundJobFilter,
} from "./background-jobs/types.js";

@customElement("jobs-jobs-jobs-background-jobs-dashboard")
export class JobsJobsJobsBackgroundJobsDashboardElement extends UmbLitElement {
  private static readonly _autoRefreshIntervalMs = 5000;

  private _authToken?: string | (() => Promise<string>);

  private _authInitialized = false;

  private _authCredentials: RequestCredentials = "include";

  private _authContext?: UmbAuthContext;

  private _autoRefreshHandle?: number;

  private readonly _api = new BackgroundJobsApi({
    getCredentials: () => this._authCredentials,
    getToken: () => this._getAuthToken(),
    refreshAuth: () => this._refreshAuth(),
  });

  @state()
  private _items: Array<BackgroundJobDashboardItem> = [];

  @state()
  private _isLoading = false;

  @state()
  private _reloadState: UUIButtonState = undefined;

  @state()
  private _runStates: Record<string, UUIButtonState> = {};

  @state()
  private _stopStates: Record<string, UUIButtonState> = {};

  @state()
  private _errorMessage = "";

  @state()
  private _statusFilter: BackgroundJobFilter = "all";

  constructor() {
    super();

    this.consumeContext(UMB_AUTH_CONTEXT, (authContext) => {
      const config = authContext?.getOpenApiConfiguration();
      this._authContext = authContext;
      this._authInitialized = authContext !== undefined;
      this._authToken = config?.token;
      this._authCredentials = config?.credentials ?? "include";
      this._load();
    });
  }

  override connectedCallback() {
    super.connectedCallback();
    if (this._items.length > 0) {
      this._startAutoRefresh();
    }
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

  private async _refreshAuth(): Promise<boolean> {
    if (this._authContext === undefined) {
      return false;
    }

    try {
      const valid = await this._authContext.validateToken();
      return valid === true;
    } catch {
      return false;
    }
  }

  private async _load() {
    if (this._authInitialized === false) {
      return;
    }

    const authToken = await this._getAuthToken();
    if (!authToken) {
      this._stopAutoRefresh();
      return;
    }

    this._isLoading = true;
    this._errorMessage = "";

    try {
      const data = await this._api.list();
      this._items = data.items ?? [];

      if (this._items.length > 0) {
        this._startAutoRefresh();
      } else {
        this._stopAutoRefresh();
      }
    } catch (error) {
      this._errorMessage = error instanceof Error ? error.message : "Could not load background jobs.";
    } finally {
      this._isLoading = false;
    }
  }

  private _reload = async () => {
    this._reloadState = "waiting";
    await this._load();
    this._reloadState = this._errorMessage ? "failed" : "success";
  };

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
      await this._api.run(alias);
      this._runStates = { ...this._runStates, [alias]: "success" };
      await this._load();
    } catch (error) {
      this._runStates = { ...this._runStates, [alias]: "failed" };
      this._errorMessage = error instanceof Error ? error.message : `Could not run ${alias}.`;
    }
  }

  private async _stopJob(alias: string) {
    this._stopStates = { ...this._stopStates, [alias]: "waiting" };
    this._errorMessage = "";

    try {
      await this._api.stop(alias);
      await this._load();
      this._stopStates = { ...this._stopStates, [alias]: undefined };
    } catch (error) {
      this._stopStates = { ...this._stopStates, [alias]: "failed" };
      this._errorMessage = error instanceof Error ? error.message : `Could not stop ${alias}.`;
    }
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

  private _onFilterChange = (event: Event) => {
    this._statusFilter = (event.target as HTMLSelectElement).value as BackgroundJobFilter;
  };

  private _hasCurrentStateDetails(item: BackgroundJobDashboardItem) {
    if (item.stopRequested || item.isRunning) {
      return true;
    }

    const currentError = normalizeText(item.lastError);
    const currentMessage = normalizeText(item.lastMessage);
    const storedError = normalizeText(item.latestRun?.error);
    const storedMessage = normalizeText(item.latestRun?.message);

    if (currentError && isSameText(currentError, storedError) === false) {
      return true;
    }

    if (currentMessage && isSameText(currentMessage, storedMessage) === false) {
      return true;
    }

    return false;
  }

  private _renderSchedule(item: BackgroundJobDashboardItem) {
    if (item.usesCronSchedule) {
      return html`
        <div>${item.scheduleDisplay ?? item.cronExpression ?? "-"}</div>
        <div class="muted">CRON · ${item.timeZoneId ?? "UTC"}</div>
      `;
    }

    return html`
      <div>${formatTimeSpan(item.period)}</div>
      <div class="muted">Interval</div>
    `;
  }

  private _renderCurrentStateDetails(item: BackgroundJobDashboardItem) {
    const currentError = normalizeText(item.lastError);
    const currentMessage = normalizeText(item.lastMessage);
    const storedError = normalizeText(item.latestRun?.error);
    const storedMessage = normalizeText(item.latestRun?.message);

    if (item.stopRequested) {
      return html`
        <div class="current-state-title-row">
          <strong>Current state</strong>
          <span class="current-state-pill current-state-pill-warning">Stop requested</span>
        </div>
        <div>Waiting for the running job to stop gracefully.</div>
        ${item.lastStartedAt ? html`<div class="muted">Started ${formatDate(item.lastStartedAt)}</div>` : ""}
        ${currentMessage ? html`<div><strong>Message:</strong> ${currentMessage}</div>` : ""}
      `;
    }

    if (item.isRunning) {
      return html`
        <div class="current-state-title-row">
          <strong>Current state</strong>
          <span class="current-state-pill current-state-pill-running">Running now</span>
        </div>
        ${item.lastStartedAt ? html`<div>Started ${formatDate(item.lastStartedAt)}</div>` : html`<div>Job is currently executing.</div>`}
        ${currentMessage ? html`<div><strong>Message:</strong> ${currentMessage}</div>` : ""}
      `;
    }

    if (currentError && isSameText(currentError, storedError) === false) {
      return html`
        <div class="current-state-title-row">
          <strong>Current state</strong>
          <span class="current-state-pill current-state-pill-error">Attention</span>
        </div>
        <div><strong>Error:</strong> ${currentError}</div>
        ${currentMessage && isSameText(currentMessage, storedMessage) === false
          ? html`<div><strong>Message:</strong> ${currentMessage}</div>`
          : ""}
      `;
    }

    if (currentMessage && isSameText(currentMessage, storedMessage) === false) {
      return html`
        <div class="current-state-title-row">
          <strong>Current state</strong>
          <span class="current-state-pill">Live update</span>
        </div>
        <div><strong>Message:</strong> ${currentMessage}</div>
      `;
    }

    return "";
  }

  private _renderLatestRun(item: BackgroundJobDashboardItem) {
    const run = item.latestRun;

    if (!run) {
      return "";
    }

    return html`
      <details class="persisted-run">
        <summary class="persisted-run-toggle">
          <span class="persisted-run-heading">
            <span class="persisted-run-indicator" aria-hidden="true">
              <span class="persisted-run-chevron">▸</span>
              <span class="muted">Details</span>
            </span>
            <strong>Latest stored run</strong>
            <span class=${getStatusClassFromValue(run.status)}>${run.status}</span>
          </span>
          <span class="muted persisted-run-toggle-meta">
            ${formatDate(run.startedAt)}
            ${run.trigger}
            ${formatDuration(run.duration)}
          </span>
        </summary>
        <div class="persisted-run-body">
          ${run.completedAt ? html`<div class="muted">Completed ${formatDate(run.completedAt)}</div>` : ""}
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
                            <span class="log-time">${formatDate(log.loggedAt)}</span>
                            <span class=${getStatusClassFromValue(log.level)}>${log.level}</span>
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
      </details>
    `;
  }

  private _renderRecentRuns(item: BackgroundJobDashboardItem) {
    const runs = item.recentRuns ?? [];

    if (runs.length === 0) {
      return "";
    }

    return html`
      <details class="persisted-run recent-runs-panel">
        <summary class="persisted-run-toggle">
          <span class="persisted-run-heading">
            <span class="persisted-run-indicator" aria-hidden="true">
              <span class="persisted-run-chevron">▸</span>
              <span class="muted">Details</span>
            </span>
            <strong>Recent stored runs</strong>
          </span>
          <span class="muted persisted-run-toggle-meta">${runs.length} shown</span>
        </summary>
        <div class="persisted-run-body">
          <ul class="recent-run-list">
            ${runs.map(
              (run) => html`
                <li class="recent-run-item">
                  <div class="recent-run-main">
                    <span class=${getStatusClassFromValue(run.status)}>${run.status}</span>
                    <span>${formatDate(run.startedAt)}</span>
                    <span class="muted">${run.trigger}</span>
                    <span class="muted">${formatDuration(run.duration)}</span>
                  </div>
                  ${run.error
                    ? html`<div class="recent-run-message"><strong>Error:</strong> ${run.error}</div>`
                    : run.message
                      ? html`<div class="recent-run-message"><strong>Message:</strong> ${run.message}</div>`
                      : ""}
                </li>
              `,
            )}
          </ul>
        </div>
      </details>
    `;
  }

  private _renderEmptyState() {
    if (this._isLoading) {
      return html`<div class="empty-state-panel">Loading jobs…</div>`;
    }

    if (this._items.length > 0) {
      return html`<div class="empty-state-panel">No jobs match the selected filter.</div>`;
    }

    return html`
      <div class="empty-state-panel">
        <div class="empty-state">
          <strong>No custom background jobs have been registered yet.</strong>
          <div class="muted">Add a class implementing <code>IRecurringBackgroundJob</code> and register it in Umbraco to see it here.</div>
          <a
            class="empty-state-link"
            href="https://github.com/SebastianFalborg/JobsJobsJobs/blob/main/docs/README_nuget.md#register-your-first-job"
            target="_blank"
            rel="noopener noreferrer">
            View setup guide
          </a>
        </div>
      </div>
    `;
  }

  private _renderRows() {
    const items = this._getVisibleItems();

    if (items.length === 0) {
      return this._renderEmptyState();
    }

    return items.map(
      (item) => html`
        <section class="job-card ${getJobCardClass(item)}">
          <div class="job-card-header">
            <div class="job-card-heading">
              <div class="job-card-title-row">
                <strong class="job-name">${item.name}</strong>
                <span class=${getStatusClass(item)}>${getStatusLabel(item)}</span>
              </div>
              <div class="muted job-meta" title=${item.type}>${item.type}</div>
            </div>
            <div class="action-buttons">
              ${item.isRunning && item.canStop
                ? html`
                    <uui-button
                      look="primary"
                      color="danger"
                      label="Stop"
                      ?disabled=${item.stopRequested}
                      .state=${this._stopStates[item.alias]}
                      @click=${() => this._stopJob(item.alias)}>
                      ${item.stopRequested ? "Stopping…" : "Stop"}
                    </uui-button>
                  `
                : html`
                    <uui-button
                      look="primary"
                      label="Run now"
                      ?disabled=${item.allowManualTrigger === false}
                      .state=${this._runStates[item.alias]}
                      @click=${() => this._runJob(item.alias)}>
                      Run now
                    </uui-button>
                  `}
            </div>
          </div>

          <div class="job-card-subheader">
            <span class="job-type-chip">${item.usesCronSchedule ? "CRON" : "Interval"}</span>
            ${item.canStop ? html`<span class="job-type-chip">Stoppable</span>` : ""}
            ${item.allowManualTrigger ? "" : html`<span class="job-type-chip">Manual run disabled</span>`}
          </div>

          <div class="job-card-grid">
            <div class="job-stat">
              <span class="job-stat-label">Last success</span>
              <span class="job-stat-value">${formatDate(item.lastSucceededAt)}</span>
            </div>
            <div class="job-stat">
              <span class="job-stat-label">Last failure</span>
              <span class="job-stat-value">${formatDate(item.lastFailedAt)}</span>
            </div>
            <div class="job-stat">
              <span class="job-stat-label">Last start</span>
              <span class="job-stat-value">${formatDate(item.lastStartedAt)}</span>
            </div>
            <div class="job-stat">
              <span class="job-stat-label">Last duration</span>
              <span class="job-stat-value">${formatDuration(item.lastDuration)}</span>
            </div>
            <div class="job-stat job-stat-schedule">
              <span class="job-stat-label">Schedule</span>
              <div class="job-stat-value">${this._renderSchedule(item)}</div>
            </div>
          </div>

          ${this._hasCurrentStateDetails(item)
            ? html`<div class="current-state-panel">${this._renderCurrentStateDetails(item)}</div>`
            : ""}

          ${item.recentRuns?.length || item.latestRun
            ? html`<div class="job-card-sections">${this._renderRecentRuns(item)}${this._renderLatestRun(item)}</div>`
            : ""}
        </section>`,
    );
  }

  override render() {
    return html`
      <uui-box headline="Background Jobs">
        <uui-button slot="header-actions" look="secondary" label="Refresh" @click=${this._reload} .state=${this._reloadState}>
          Refresh
        </uui-button>
        <p>Recurring background jobs registered in Umbraco with status, schedule, and manual trigger. The schedule column shows either CRON or the recurring interval.</p>
        <p class="muted refresh-info">Auto-refreshes every ${JobsJobsJobsBackgroundJobsDashboardElement._autoRefreshIntervalMs / 1000} seconds when custom jobs are present.</p>
        <p class="muted refresh-info">Run timestamps are shown in ${getViewerTimeZone()}. CRON schedules use the configured job timezone.</p>
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
        <div class="job-card-list">
          ${this._renderRows()}
        </div>
      </uui-box>
    `;
  }

  static override styles = [UmbTextStyles, backgroundJobsDashboardStyles];
}

export default JobsJobsJobsBackgroundJobsDashboardElement;

declare global {
  interface HTMLElementTagNameMap {
    "jobs-jobs-jobs-background-jobs-dashboard": JobsJobsJobsBackgroundJobsDashboardElement;
  }
}
