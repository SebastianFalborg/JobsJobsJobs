import { html as t, css as g, state as l, customElement as h } from "@umbraco-cms/backoffice/external/lit";
import { UMB_AUTH_CONTEXT as b } from "@umbraco-cms/backoffice/auth";
import { UmbLitElement as v } from "@umbraco-cms/backoffice/lit-element";
import { UmbTextStyles as m } from "@umbraco-cms/backoffice/style";
var f = Object.defineProperty, _ = Object.getOwnPropertyDescriptor, u = (s, e, r, a) => {
  for (var i = a > 1 ? void 0 : a ? _(e, r) : e, n = s.length - 1, d; n >= 0; n--)
    (d = s[n]) && (i = (a ? d(e, r, i) : d(i)) || i);
  return a && i && f(e, r, i), i;
};
let o = class extends v {
  constructor() {
    super(), this._authInitialized = !1, this._authCredentials = "include", this._items = [], this._isLoading = !1, this._reloadState = void 0, this._runStates = {}, this._stopStates = {}, this._errorMessage = "", this._statusFilter = "all", this.consumeContext(b, (s) => {
      const e = s?.getOpenApiConfiguration();
      this._authInitialized = s !== void 0, this._authToken = e?.token, this._authCredentials = e?.credentials ?? "include", this._load();
    });
  }
  connectedCallback() {
    super.connectedCallback(), this._items.length > 0 && this._startAutoRefresh();
  }
  disconnectedCallback() {
    this._stopAutoRefresh(), super.disconnectedCallback();
  }
  async _getAuthToken() {
    return typeof this._authToken == "function" ? this._authToken() : this._authToken;
  }
  async _load() {
    if (this._authInitialized === !1)
      return;
    const s = await this._getAuthToken();
    if (!s) {
      this._stopAutoRefresh();
      return;
    }
    this._isLoading = !0, this._errorMessage = "";
    try {
      const e = await this._fetch(
        "/umbraco/jobsjobsjobs/api/v1/background-jobs",
        s,
        {
          method: "GET"
        }
      );
      if (!e.ok)
        throw new Error(await this._readProblem(e));
      const r = await e.json();
      this._items = r.items ?? [], this._items.length > 0 ? this._startAutoRefresh() : this._stopAutoRefresh();
    } catch (e) {
      this._errorMessage = e instanceof Error ? e.message : "Could not load background jobs.";
    } finally {
      this._isLoading = !1;
    }
  }
  async _reload() {
    this._reloadState = "waiting", await this._load(), this._reloadState = this._errorMessage ? "failed" : "success";
  }
  _startAutoRefresh() {
    this._stopAutoRefresh(), this._autoRefreshHandle = window.setInterval(() => {
      this._autoRefresh();
    }, o._autoRefreshIntervalMs);
  }
  _stopAutoRefresh() {
    this._autoRefreshHandle !== void 0 && (window.clearInterval(this._autoRefreshHandle), this._autoRefreshHandle = void 0);
  }
  async _autoRefresh() {
    this._isLoading || await this._load();
  }
  async _runJob(s) {
    this._runStates = { ...this._runStates, [s]: "waiting" }, this._errorMessage = "";
    try {
      const e = await this._getAuthToken();
      if (!e)
        throw new Error("Backoffice authentication is not ready yet.");
      const r = await this._fetch(
        `/umbraco/jobsjobsjobs/api/v1/background-jobs/run/${encodeURIComponent(s)}`,
        e,
        {
          method: "POST"
        }
      );
      if (!r.ok)
        throw new Error(await this._readProblem(r));
      this._runStates = { ...this._runStates, [s]: "success" }, await this._load();
    } catch (e) {
      this._runStates = { ...this._runStates, [s]: "failed" }, this._errorMessage = e instanceof Error ? e.message : `Could not run ${s}.`;
    }
  }
  async _stopJob(s) {
    this._stopStates = { ...this._stopStates, [s]: "waiting" }, this._errorMessage = "";
    try {
      const e = await this._getAuthToken();
      if (!e)
        throw new Error("Backoffice authentication is not ready yet.");
      const r = await this._fetch(
        `/umbraco/jobsjobsjobs/api/v1/background-jobs/stop/${encodeURIComponent(s)}`,
        e,
        {
          method: "POST"
        }
      );
      if (!r.ok)
        throw new Error(await this._readProblem(r));
      await this._load(), this._stopStates = { ...this._stopStates, [s]: void 0 };
    } catch (e) {
      this._stopStates = { ...this._stopStates, [s]: "failed" }, this._errorMessage = e instanceof Error ? e.message : `Could not stop ${s}.`;
    }
  }
  async _fetch(s, e, r) {
    const a = new Headers(r?.headers);
    return a.set("Content-Type", "application/json"), a.set("Authorization", `Bearer ${e}`), fetch(s, {
      ...r,
      credentials: this._authCredentials,
      headers: a
    });
  }
  async _readProblem(s) {
    try {
      const e = await s.json();
      return e.detail ?? e.title ?? e.message ?? `Request failed with status ${s.status}.`;
    } catch {
      return `Request failed with status ${s.status}.`;
    }
  }
  _formatDate(s) {
    if (!s) return "-";
    const e = new Date(s);
    return Number.isNaN(e.getTime()) ? s : e.toLocaleString();
  }
  _formatTimeSpan(s) {
    return s.startsWith("00:") || s.startsWith("0."), s;
  }
  _renderSchedule(s) {
    return s.usesCronSchedule ? t`
        <div>${s.scheduleDisplay ?? s.cronExpression ?? "-"}</div>
        <div class="muted">CRON · ${s.timeZoneId ?? "UTC"}</div>
      ` : t`
      <div>${this._formatTimeSpan(s.period)}</div>
      <div class="muted">Interval</div>
    `;
  }
  _formatDuration(s) {
    if (!s) return "-";
    const e = /^(?:(\d+)\.)?(\d{2}):(\d{2}):(\d{2})(?:\.(\d+))?$/.exec(s);
    if (!e)
      return s;
    const r = Number(e[1] ?? "0"), a = Number(e[2]), i = Number(e[3]), n = Number(e[4]), d = e[5] ?? "", p = d ? Math.round(+`0.${d}` * 1e3) : 0;
    if (r > 0 || a > 0 || i > 0) {
      const c = [];
      return r > 0 && c.push(`${r}d`), a > 0 && c.push(`${a}h`), i > 0 && c.push(`${i}m`), n > 0 && c.push(`${n}s`), c.join(" ");
    }
    return n > 0 ? p === 0 ? `${n}s` : `${n}.${p.toString().padStart(3, "0").replace(/0+$/, "")}s` : `${p}ms`;
  }
  _getStatusLabel(s) {
    return s.stopRequested ? "StopRequested" : s.isRunning ? "Running" : s.lastStatus;
  }
  _getStatusClassFromValue(s) {
    return `status-badge status-${s.toLowerCase()}`;
  }
  _getStatusClass(s) {
    return this._getStatusClassFromValue(this._getStatusLabel(s));
  }
  _normalizeText(s) {
    const e = s?.trim();
    return e || void 0;
  }
  _isSameText(s, e) {
    return this._normalizeText(s) === this._normalizeText(e);
  }
  _hasCurrentStateDetails(s) {
    if (s.stopRequested || s.isRunning)
      return !0;
    const e = this._normalizeText(s.lastError), r = this._normalizeText(s.lastMessage), a = this._normalizeText(s.latestRun?.error), i = this._normalizeText(s.latestRun?.message);
    return !!(e && this._isSameText(e, a) === !1 || r && this._isSameText(r, i) === !1);
  }
  _renderCurrentStateDetails(s) {
    const e = this._normalizeText(s.lastError), r = this._normalizeText(s.lastMessage), a = this._normalizeText(s.latestRun?.error), i = this._normalizeText(s.latestRun?.message);
    return s.stopRequested ? t`
        <div class="current-state-title-row">
          <strong>Current state</strong>
          <span class="current-state-pill current-state-pill-warning">Stop requested</span>
        </div>
        <div>Waiting for the running job to stop gracefully.</div>
        ${s.lastStartedAt ? t`<div class="muted">Started ${this._formatDate(s.lastStartedAt)}</div>` : ""}
        ${r ? t`<div><strong>Message:</strong> ${r}</div>` : ""}
      ` : s.isRunning ? t`
        <div class="current-state-title-row">
          <strong>Current state</strong>
          <span class="current-state-pill current-state-pill-running">Running now</span>
        </div>
        ${s.lastStartedAt ? t`<div>Started ${this._formatDate(s.lastStartedAt)}</div>` : t`<div>Job is currently executing.</div>`}
        ${r ? t`<div><strong>Message:</strong> ${r}</div>` : ""}
      ` : e && this._isSameText(e, a) === !1 ? t`
        <div class="current-state-title-row">
          <strong>Current state</strong>
          <span class="current-state-pill current-state-pill-error">Attention</span>
        </div>
        <div><strong>Error:</strong> ${e}</div>
        ${r && this._isSameText(r, i) === !1 ? t`<div><strong>Message:</strong> ${r}</div>` : ""}
      ` : r && this._isSameText(r, i) === !1 ? t`
        <div class="current-state-title-row">
          <strong>Current state</strong>
          <span class="current-state-pill">Live update</span>
        </div>
        <div><strong>Message:</strong> ${r}</div>
      ` : "";
  }
  _matchesFilter(s) {
    switch (this._statusFilter) {
      case "running":
        return s.isRunning;
      case "failed":
        return s.lastStatus === "Failed";
      case "succeeded":
        return s.lastStatus === "Succeeded";
      case "idle":
        return s.lastStatus === "Idle" && s.isRunning === !1;
      default:
        return !0;
    }
  }
  _getJobCardClass(s) {
    return s.isRunning ? "job-card-running" : s.lastStatus === "Failed" ? "job-card-failed" : s.lastStatus === "Succeeded" ? "job-card-succeeded" : !s.lastStartedAt && !s.latestRun ? "job-card-never-run" : "";
  }
  _getVisibleItems() {
    return this._items.filter((s) => this._matchesFilter(s));
  }
  _onFilterChange(s) {
    this._statusFilter = s.target.value;
  }
  _renderLatestRun(s) {
    const e = s.latestRun;
    return e ? t`
      <details class="persisted-run">
        <summary class="persisted-run-toggle">
          <span class="persisted-run-heading">
            <span class="persisted-run-indicator" aria-hidden="true">
              <span class="persisted-run-chevron">▸</span>
              <span class="muted">Details</span>
            </span>
            <strong>Latest stored run</strong>
            <span class=${this._getStatusClassFromValue(e.status)}>${e.status}</span>
          </span>
          <span class="muted persisted-run-toggle-meta">
            ${this._formatDate(e.startedAt)}
            ${e.trigger}
            ${this._formatDuration(e.duration)}
          </span>
        </summary>
        <div class="persisted-run-body">
          ${e.completedAt ? t`<div class="muted">Completed ${this._formatDate(e.completedAt)}</div>` : ""}
          ${e.error ? t`<div><strong>Stored error:</strong> ${e.error}</div>` : e.message ? t`<div><strong>Stored message:</strong> ${e.message}</div>` : ""}
          ${e.logs.length > 0 ? t`
                <div class="persisted-run-logs">
                  <strong>Stored logs</strong>
                  <ul class="log-list">
                    ${e.logs.map(
      (r) => t`
                        <li class="log-item">
                          <div class="log-meta">
                            <span class="log-time">${this._formatDate(r.loggedAt)}</span>
                            <span class=${this._getStatusClassFromValue(r.level)}>${r.level}</span>
                          </div>
                          <span class="log-message">${r.message}</span>
                        </li>
                      `
    )}
                  </ul>
                </div>
              ` : t`<div class="muted">No stored logs for the latest run.</div>`}
        </div>
      </details>
    ` : "";
  }
  _renderRecentRuns(s) {
    const e = s.recentRuns ?? [];
    return e.length === 0 ? "" : t`
      <details class="persisted-run recent-runs-panel">
        <summary class="persisted-run-toggle">
          <span class="persisted-run-heading">
            <span class="persisted-run-indicator" aria-hidden="true">
              <span class="persisted-run-chevron">▸</span>
              <span class="muted">Details</span>
            </span>
            <strong>Recent stored runs</strong>
          </span>
          <span class="muted persisted-run-toggle-meta">${e.length} shown</span>
        </summary>
        <div class="persisted-run-body">
          <ul class="recent-run-list">
            ${e.map(
      (r) => t`
                <li class="recent-run-item">
                  <div class="recent-run-main">
                    <span class=${this._getStatusClassFromValue(r.status)}>${r.status}</span>
                    <span>${this._formatDate(r.startedAt)}</span>
                    <span class="muted">${r.trigger}</span>
                    <span class="muted">${this._formatDuration(r.duration)}</span>
                  </div>
                  ${r.error ? t`<div class="recent-run-message"><strong>Error:</strong> ${r.error}</div>` : r.message ? t`<div class="recent-run-message"><strong>Message:</strong> ${r.message}</div>` : ""}
                </li>
              `
    )}
          </ul>
        </div>
      </details>
    `;
  }
  _renderEmptyState() {
    return this._isLoading ? t`<div class="empty-state-panel">Loading jobs…</div>` : this._items.length > 0 ? t`<div class="empty-state-panel">No jobs match the selected filter.</div>` : t`
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
  _renderRows() {
    const s = this._getVisibleItems();
    return s.length === 0 ? this._renderEmptyState() : s.map(
      (e) => t`
        <section class="job-card ${this._getJobCardClass(e)}">
          <div class="job-card-header">
            <div class="job-card-heading">
              <div class="job-card-title-row">
                <strong class="job-name">${e.name}</strong>
                <span class=${this._getStatusClass(e)}>${this._getStatusLabel(e)}</span>
              </div>
              <div class="muted job-meta" title=${e.type}>${e.type}</div>
            </div>
            <div class="action-buttons">
              ${e.isRunning && e.canStop ? t`
                    <uui-button
                      look="primary"
                      color="danger"
                      label="Stop"
                      ?disabled=${e.stopRequested}
                      .state=${this._stopStates[e.alias]}
                      @click=${() => this._stopJob(e.alias)}>
                      ${e.stopRequested ? "Stopping…" : "Stop"}
                    </uui-button>
                  ` : t`
                    <uui-button
                      look="primary"
                      label="Run now"
                      ?disabled=${e.allowManualTrigger === !1}
                      .state=${this._runStates[e.alias]}
                      @click=${() => this._runJob(e.alias)}>
                      Run now
                    </uui-button>
                  `}
            </div>
          </div>

          <div class="job-card-subheader">
            <span class="job-type-chip">${e.usesCronSchedule ? "CRON" : "Interval"}</span>
            ${e.canStop ? t`<span class="job-type-chip">Stoppable</span>` : ""}
            ${e.allowManualTrigger ? "" : t`<span class="job-type-chip">Manual run disabled</span>`}
          </div>

          <div class="job-card-grid">
            <div class="job-stat">
              <span class="job-stat-label">Last success</span>
              <span class="job-stat-value">${this._formatDate(e.lastSucceededAt)}</span>
            </div>
            <div class="job-stat">
              <span class="job-stat-label">Last failure</span>
              <span class="job-stat-value">${this._formatDate(e.lastFailedAt)}</span>
            </div>
            <div class="job-stat">
              <span class="job-stat-label">Last start</span>
              <span class="job-stat-value">${this._formatDate(e.lastStartedAt)}</span>
            </div>
            <div class="job-stat">
              <span class="job-stat-label">Last duration</span>
              <span class="job-stat-value">${this._formatDuration(e.lastDuration)}</span>
            </div>
            <div class="job-stat job-stat-schedule">
              <span class="job-stat-label">Schedule</span>
              <div class="job-stat-value">${this._renderSchedule(e)}</div>
            </div>
          </div>

          ${this._hasCurrentStateDetails(e) ? t`<div class="current-state-panel">${this._renderCurrentStateDetails(e)}</div>` : ""}

          ${e.recentRuns?.length || e.latestRun ? t`<div class="job-card-sections">${this._renderRecentRuns(e)}${this._renderLatestRun(e)}</div>` : ""}
        </section>`
    );
  }
  render() {
    return t`
      <uui-box headline="Background Jobs">
        <uui-button slot="header-actions" look="secondary" label="Refresh" @click=${this._reload} .state=${this._reloadState}>
          Refresh
        </uui-button>
        <p>Recurring background jobs registered in Umbraco with status, schedule, and manual trigger. The schedule column shows either CRON or the recurring interval.</p>
        <p class="muted refresh-info">Auto-refreshes every ${o._autoRefreshIntervalMs / 1e3} seconds when custom jobs are present.</p>
        ${this._errorMessage ? t`<p class="error">${this._errorMessage}</p>` : ""}
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
};
o._autoRefreshIntervalMs = 5e3;
o.styles = [
  m,
  g`
      :host {
        display: block;
        padding: var(--uui-size-layout-1);
      }

      .job-card-list {
        display: grid;
        gap: var(--uui-size-space-4);
      }

      .muted {
        color: var(--uui-color-text-alt);
        font-size: var(--uui-type-small-size);
      }

      .refresh-info {
        margin-top: calc(var(--uui-size-space-1) * -1);
      }

      .empty-state {
        display: flex;
        flex-direction: column;
        gap: var(--uui-size-space-2);
      }

      .empty-state-panel {
        padding: var(--uui-size-space-4);
        border: 1px solid var(--uui-color-border);
        border-radius: var(--uui-border-radius);
        background: var(--uui-color-surface);
      }

      .empty-state-link {
        width: fit-content;
        color: var(--uui-color-interactive-emphasis);
        text-decoration: none;
        font-weight: 600;
      }

      .empty-state-link:hover {
        text-decoration: underline;
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

      .job-card {
        display: grid;
        gap: var(--uui-size-space-4);
        padding: var(--uui-size-space-4);
        border: 1px solid var(--uui-color-border);
        border-radius: calc(var(--uui-border-radius) * 1.5);
        background:
          linear-gradient(180deg, color-mix(in srgb, var(--uui-color-surface-alt) 65%, white) 0%, var(--uui-color-surface) 100%);
        box-shadow:
          0 1px 2px color-mix(in srgb, var(--uui-color-border) 40%, transparent),
          0 14px 32px color-mix(in srgb, var(--uui-color-border) 10%, transparent);
        position: relative;
        overflow: hidden;
      }

      .job-card::before {
        content: "";
        position: absolute;
        inset: 0 auto 0 0;
        width: 6px;
        background: linear-gradient(180deg, var(--uui-color-interactive-emphasis), color-mix(in srgb, var(--uui-color-border-emphasis) 65%, white));
      }

      .job-card-running::before {
        background: linear-gradient(180deg, var(--uui-color-warning-emphasis), var(--uui-color-warning));
      }

      .job-card-failed::before {
        background: linear-gradient(180deg, var(--uui-color-danger-emphasis), var(--uui-color-danger));
      }

      .job-card-succeeded::before {
        background: linear-gradient(180deg, var(--uui-color-positive-emphasis), var(--uui-color-positive));
      }

      .job-card-never-run::before {
        background: linear-gradient(180deg, var(--uui-color-interactive-emphasis), var(--uui-color-interactive));
      }

      .job-card-header {
        display: flex;
        flex-wrap: wrap;
        justify-content: space-between;
        gap: var(--uui-size-space-3);
      }

      .job-card-heading {
        display: grid;
        gap: var(--uui-size-space-1);
        min-width: 0;
        flex: 1 1 24rem;
      }

      .job-card-title-row {
        display: flex;
        flex-wrap: wrap;
        align-items: center;
        gap: var(--uui-size-space-2);
      }

      .job-card-subheader {
        display: flex;
        flex-wrap: wrap;
        gap: var(--uui-size-space-2);
      }

      .job-type-chip {
        display: inline-flex;
        align-items: center;
        min-height: 1.85rem;
        padding: 0.2rem 0.65rem;
        border: 1px solid color-mix(in srgb, var(--uui-color-border) 80%, transparent);
        border-radius: 999px;
        background: color-mix(in srgb, var(--uui-color-surface-alt) 82%, white);
        color: var(--uui-color-text-alt);
        font-size: var(--uui-type-small-size);
        font-weight: 600;
      }

      .job-name {
        display: block;
        overflow-wrap: anywhere;
        word-break: break-word;
        line-height: 1.35;
        font-size: 1.05rem;
      }

      .job-meta {
        display: block;
        max-width: 100%;
        overflow: hidden;
        text-overflow: ellipsis;
        white-space: nowrap;
      }

      .job-card-grid {
        display: grid;
        grid-template-columns: repeat(auto-fit, minmax(10rem, 1fr));
        gap: var(--uui-size-space-3);
      }

      .job-stat {
        display: grid;
        gap: var(--uui-size-space-1);
        padding: var(--uui-size-space-3);
        border-radius: var(--uui-border-radius);
        background: linear-gradient(180deg, color-mix(in srgb, var(--uui-color-surface-alt) 88%, white) 0%, var(--uui-color-surface-alt) 100%);
        border: 1px solid color-mix(in srgb, var(--uui-color-border) 75%, transparent);
        box-shadow: inset 0 1px 0 color-mix(in srgb, white 70%, transparent);
      }

      .job-stat-schedule {
        grid-column: span 2;
      }

      .job-stat-label {
        font-size: var(--uui-type-small-size);
        color: var(--uui-color-text-alt);
      }

      .job-stat-value {
        min-width: 0;
        overflow-wrap: anywhere;
        word-break: break-word;
      }

      .current-state-panel {
        display: grid;
        gap: var(--uui-size-space-2);
        padding: var(--uui-size-space-3);
        border: 1px solid color-mix(in srgb, var(--uui-color-warning) 35%, var(--uui-color-border));
        border-left: 5px solid var(--uui-color-warning-emphasis);
        background: linear-gradient(180deg, color-mix(in srgb, var(--uui-color-warning) 7%, white) 0%, var(--uui-color-surface-alt) 100%);
        border-radius: var(--uui-border-radius);
      }

      .current-state-title-row {
        display: flex;
        flex-wrap: wrap;
        align-items: center;
        justify-content: space-between;
        gap: var(--uui-size-space-2);
      }

      .current-state-pill {
        display: inline-flex;
        align-items: center;
        padding: 0.2rem 0.55rem;
        border-radius: 999px;
        background: color-mix(in srgb, var(--uui-color-interactive-emphasis) 12%, white);
        color: var(--uui-color-interactive-emphasis);
        font-size: var(--uui-type-small-size);
        font-weight: 700;
      }

      .current-state-pill-running {
        background: color-mix(in srgb, var(--uui-color-warning) 18%, white);
        color: var(--uui-color-warning-emphasis);
      }

      .current-state-pill-warning {
        background: color-mix(in srgb, var(--uui-color-warning) 22%, white);
        color: var(--uui-color-warning-emphasis);
      }

      .current-state-pill-error {
        background: color-mix(in srgb, var(--uui-color-danger) 18%, white);
        color: var(--uui-color-danger-emphasis);
      }

      .job-card-sections {
        display: grid;
        gap: var(--uui-size-space-3);
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

      .status-stoprequested {
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
      .status-stopped,
      .status-ignored {
        background: var(--uui-color-surface-alt);
        color: var(--uui-color-text);
      }

      .action-buttons {
        display: flex;
        flex-wrap: wrap;
        gap: var(--uui-size-space-2);
        justify-content: flex-end;
        align-items: flex-start;
      }

      @media (max-width: 1200px) {
        .job-stat-schedule {
          grid-column: span 1;
        }
      }

      @media (max-width: 720px) {
        .job-card {
          padding: var(--uui-size-space-3);
        }

        .job-card-grid {
          grid-template-columns: 1fr;
        }
      }

      .persisted-run {
        display: grid;
        gap: var(--uui-size-space-2);
        padding: var(--uui-size-space-3);
        border: 1px solid var(--uui-color-border);
        border-radius: var(--uui-border-radius);
        background: linear-gradient(180deg, color-mix(in srgb, var(--uui-color-surface-alt) 92%, white) 0%, var(--uui-color-surface-alt) 100%);
        box-shadow: inset 0 1px 0 color-mix(in srgb, white 65%, transparent);
      }

      .recent-runs {
        display: grid;
        gap: var(--uui-size-space-2);
      }

      .recent-runs-header {
        display: flex;
        flex-wrap: wrap;
        align-items: center;
        justify-content: space-between;
        gap: var(--uui-size-space-2);
      }

      .recent-run-list {
        list-style: none;
        margin: 0;
        padding: 0;
        display: grid;
        gap: var(--uui-size-space-2);
      }

      .recent-run-item {
        display: grid;
        gap: var(--uui-size-space-1);
        padding: var(--uui-size-space-2) var(--uui-size-space-3);
        background: linear-gradient(180deg, color-mix(in srgb, var(--uui-color-surface) 90%, white) 0%, var(--uui-color-surface) 100%);
        border-radius: var(--uui-border-radius);
        border: 1px solid color-mix(in srgb, var(--uui-color-border) 85%, transparent);
      }

      .recent-run-main {
        display: flex;
        flex-wrap: wrap;
        align-items: center;
        gap: var(--uui-size-space-2);
      }

      .recent-run-message {
        white-space: pre-wrap;
        word-break: break-word;
      }

      .persisted-run-toggle {
        display: flex;
        flex-wrap: wrap;
        align-items: center;
        justify-content: space-between;
        gap: var(--uui-size-space-2);
        cursor: pointer;
      }

      .persisted-run-toggle-meta {
        display: inline-flex;
        flex-wrap: wrap;
        gap: var(--uui-size-space-2);
      }

      .persisted-run-body {
        display: grid;
        gap: var(--uui-size-space-2);
        padding-top: var(--uui-size-space-2);
      }

      .persisted-run-heading {
        display: flex;
        flex-wrap: wrap;
        align-items: center;
        gap: var(--uui-size-space-3);
      }

      .persisted-run-indicator {
        display: inline-flex;
        align-items: center;
        gap: var(--uui-size-space-1);
      }

      .persisted-run-chevron {
        display: inline-block;
        transition: transform 120ms ease;
      }

      .persisted-run[open] .persisted-run-chevron {
        transform: rotate(90deg);
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
        border: 1px solid color-mix(in srgb, var(--uui-color-border) 85%, transparent);
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
    `
];
u([
  l()
], o.prototype, "_items", 2);
u([
  l()
], o.prototype, "_isLoading", 2);
u([
  l()
], o.prototype, "_reloadState", 2);
u([
  l()
], o.prototype, "_runStates", 2);
u([
  l()
], o.prototype, "_stopStates", 2);
u([
  l()
], o.prototype, "_errorMessage", 2);
u([
  l()
], o.prototype, "_statusFilter", 2);
o = u([
  h("jobs-jobs-jobs-background-jobs-dashboard")
], o);
const j = o;
export {
  o as JobsJobsJobsBackgroundJobsDashboardElement,
  j as default
};
//# sourceMappingURL=background-jobs-dashboard.element-DtCLT27W.js.map
