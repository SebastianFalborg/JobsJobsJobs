import { html as r, css as h, state as l, customElement as g } from "@umbraco-cms/backoffice/external/lit";
import { UMB_AUTH_CONTEXT as b } from "@umbraco-cms/backoffice/auth";
import { UmbLitElement as _ } from "@umbraco-cms/backoffice/lit-element";
import { UmbTextStyles as f } from "@umbraco-cms/backoffice/style";
var m = Object.defineProperty, v = Object.getOwnPropertyDescriptor, u = (e, t, s, i) => {
  for (var a = i > 1 ? void 0 : i ? v(t, s) : t, n = e.length - 1, d; n >= 0; n--)
    (d = e[n]) && (a = (i ? d(t, s, a) : d(a)) || a);
  return i && a && m(t, s, a), a;
};
let o = class extends _ {
  constructor() {
    super(), this._authCredentials = "include", this._items = [], this._isLoading = !1, this._reloadState = void 0, this._runStates = {}, this._stopStates = {}, this._errorMessage = "", this._statusFilter = "all", this.consumeContext(b, (e) => {
      const t = e?.getOpenApiConfiguration();
      this._authToken = t?.token, this._authCredentials = t?.credentials ?? "include", this._load();
    });
  }
  connectedCallback() {
    super.connectedCallback(), this._startAutoRefresh();
  }
  disconnectedCallback() {
    this._stopAutoRefresh(), super.disconnectedCallback();
  }
  async _getAuthToken() {
    return typeof this._authToken == "function" ? this._authToken() : this._authToken;
  }
  async _load() {
    this._isLoading = !0, this._errorMessage = "";
    try {
      const e = await this._fetch("/umbraco/jobsjobsjobs/api/v1/background-jobs", {
        method: "GET"
      });
      if (!e.ok)
        throw new Error(await this._readProblem(e));
      const t = await e.json();
      this._items = t.items ?? [], this._items.length > 0 ? this._startAutoRefresh() : this._stopAutoRefresh();
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
  async _runJob(e) {
    this._runStates = { ...this._runStates, [e]: "waiting" }, this._errorMessage = "";
    try {
      const t = await this._fetch(`/umbraco/jobsjobsjobs/api/v1/background-jobs/run/${encodeURIComponent(e)}`, {
        method: "POST"
      });
      if (!t.ok)
        throw new Error(await this._readProblem(t));
      this._runStates = { ...this._runStates, [e]: "success" }, await this._load();
    } catch (t) {
      this._runStates = { ...this._runStates, [e]: "failed" }, this._errorMessage = t instanceof Error ? t.message : `Could not run ${e}.`;
    }
  }
  async _stopJob(e) {
    this._stopStates = { ...this._stopStates, [e]: "waiting" }, this._errorMessage = "";
    try {
      const t = await this._fetch(`/umbraco/jobsjobsjobs/api/v1/background-jobs/stop/${encodeURIComponent(e)}`, {
        method: "POST"
      });
      if (!t.ok)
        throw new Error(await this._readProblem(t));
      await this._load(), this._stopStates = { ...this._stopStates, [e]: void 0 };
    } catch (t) {
      this._stopStates = { ...this._stopStates, [e]: "failed" }, this._errorMessage = t instanceof Error ? t.message : `Could not stop ${e}.`;
    }
  }
  async _fetch(e, t) {
    const s = new Headers(t?.headers);
    s.set("Content-Type", "application/json");
    const i = await this._getAuthToken();
    return i && s.set("Authorization", `Bearer ${i}`), fetch(e, {
      ...t,
      credentials: this._authCredentials,
      headers: s
    });
  }
  async _readProblem(e) {
    try {
      const t = await e.json();
      return t.detail ?? t.title ?? t.message ?? `Request failed with status ${e.status}.`;
    } catch {
      return `Request failed with status ${e.status}.`;
    }
  }
  _formatDate(e) {
    if (!e) return "-";
    const t = new Date(e);
    return Number.isNaN(t.getTime()) ? e : t.toLocaleString();
  }
  _formatTimeSpan(e) {
    return e.startsWith("00:") || e.startsWith("0."), e;
  }
  _renderSchedule(e) {
    return e.usesCronSchedule ? r`
        <div>${e.scheduleDisplay ?? e.cronExpression ?? "-"}</div>
        <div class="muted">${e.timeZoneId ?? "UTC"}</div>
      ` : r`${this._formatTimeSpan(e.period)}`;
  }
  _formatDuration(e) {
    if (!e) return "-";
    const t = /^(?:(\d+)\.)?(\d{2}):(\d{2}):(\d{2})(?:\.(\d+))?$/.exec(e);
    if (!t)
      return e;
    const s = Number(t[1] ?? "0"), i = Number(t[2]), a = Number(t[3]), n = Number(t[4]), d = t[5] ?? "", p = d ? Math.round(+`0.${d}` * 1e3) : 0;
    if (s > 0 || i > 0 || a > 0) {
      const c = [];
      return s > 0 && c.push(`${s}d`), i > 0 && c.push(`${i}h`), a > 0 && c.push(`${a}m`), n > 0 && c.push(`${n}s`), c.join(" ");
    }
    return n > 0 ? p === 0 ? `${n}s` : `${n}.${p.toString().padStart(3, "0").replace(/0+$/, "")}s` : `${p}ms`;
  }
  _getStatusLabel(e) {
    return e.stopRequested ? "StopRequested" : e.isRunning ? "Running" : e.lastStatus;
  }
  _getStatusClassFromValue(e) {
    return `status-badge status-${e.toLowerCase()}`;
  }
  _getStatusClass(e) {
    return this._getStatusClassFromValue(this._getStatusLabel(e));
  }
  _normalizeText(e) {
    const t = e?.trim();
    return t || void 0;
  }
  _isSameText(e, t) {
    return this._normalizeText(e) === this._normalizeText(t);
  }
  _renderCurrentStateDetails(e) {
    const t = this._normalizeText(e.lastError), s = this._normalizeText(e.lastMessage), i = this._normalizeText(e.latestRun?.error), a = this._normalizeText(e.latestRun?.message);
    return t && this._isSameText(t, i) === !1 ? r`
        <div><strong>Error:</strong> ${t}</div>
        ${s && this._isSameText(s, a) === !1 ? r`<div><strong>Message:</strong> ${s}</div>` : ""}
      ` : s && this._isSameText(s, a) === !1 ? r`<div><strong>Message:</strong> ${s}</div>` : "";
  }
  _matchesFilter(e) {
    switch (this._statusFilter) {
      case "running":
        return e.isRunning;
      case "failed":
        return e.lastStatus === "Failed";
      case "succeeded":
        return e.lastStatus === "Succeeded";
      case "idle":
        return e.lastStatus === "Idle" && e.isRunning === !1;
      default:
        return !0;
    }
  }
  _getVisibleItems() {
    return this._items.filter((e) => this._matchesFilter(e));
  }
  _onFilterChange(e) {
    this._statusFilter = e.target.value;
  }
  _renderLatestRun(e) {
    const t = e.latestRun;
    return t ? r`
      <details class="persisted-run">
        <summary class="persisted-run-toggle">
          <span class="persisted-run-heading">
            <span class="persisted-run-indicator" aria-hidden="true">
              <span class="persisted-run-chevron">▸</span>
              <span class="muted">Details</span>
            </span>
            <strong>Latest stored run</strong>
            <span class=${this._getStatusClassFromValue(t.status)}>${t.status}</span>
          </span>
          <span class="muted persisted-run-toggle-meta">
            ${this._formatDate(t.startedAt)}
            ${t.trigger}
            ${this._formatDuration(t.duration)}
          </span>
        </summary>
        <div class="persisted-run-body">
          ${t.completedAt ? r`<div class="muted">Completed ${this._formatDate(t.completedAt)}</div>` : ""}
          ${t.error ? r`<div><strong>Stored error:</strong> ${t.error}</div>` : t.message ? r`<div><strong>Stored message:</strong> ${t.message}</div>` : ""}
          ${t.logs.length > 0 ? r`
                <div class="persisted-run-logs">
                  <strong>Stored logs</strong>
                  <ul class="log-list">
                    ${t.logs.map(
      (s) => r`
                        <li class="log-item">
                          <div class="log-meta">
                            <span class="log-time">${this._formatDate(s.loggedAt)}</span>
                            <span class=${this._getStatusClassFromValue(s.level)}>${s.level}</span>
                          </div>
                          <span class="log-message">${s.message}</span>
                        </li>
                      `
    )}
                  </ul>
                </div>
              ` : r`<div class="muted">No stored logs for the latest run.</div>`}
        </div>
      </details>
    ` : "";
  }
  _renderEmptyState() {
    return this._isLoading ? r`<tr><td colspan="8">Loading jobs…</td></tr>` : this._items.length > 0 ? r`<tr><td colspan="8">No jobs match the selected filter.</td></tr>` : r`
      <tr>
        <td colspan="8">
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
        </td>
      </tr>
    `;
  }
  _renderRows() {
    const e = this._getVisibleItems();
    return e.length === 0 ? this._renderEmptyState() : e.map(
      (t) => r`
        <tr>
          <td class="job-cell">
            <strong>${t.name}</strong>
            <div class="muted job-meta" title=${t.type}>${t.type}</div>
          </td>
          <td><span class=${this._getStatusClass(t)}>${this._getStatusLabel(t)}</span></td>
          <td>${this._formatDate(t.lastSucceededAt)}</td>
          <td>${this._formatDate(t.lastFailedAt)}</td>
          <td>${this._formatDate(t.lastStartedAt)}</td>
          <td>${this._formatDuration(t.lastDuration)}</td>
          <td>${this._renderSchedule(t)}</td>
          <td>
            <div class="action-buttons">
              ${t.isRunning && t.canStop ? r`
                    <uui-button
                      look="primary"
                      color="danger"
                      label="Stop"
                      ?disabled=${t.stopRequested}
                      .state=${this._stopStates[t.alias]}
                      @click=${() => this._stopJob(t.alias)}>
                      ${t.stopRequested ? "Stopping…" : "Stop"}
                    </uui-button>
                  ` : r`
                    <uui-button
                      look="primary"
                      label="Run now"
                      ?disabled=${t.allowManualTrigger === !1}
                      .state=${this._runStates[t.alias]}
                      @click=${() => this._runJob(t.alias)}>
                      Run now
                    </uui-button>
                  `}
            </div>
          </td>
        </tr>
        ${t.lastError || t.lastMessage || t.latestRun ? r`<tr class="details"><td colspan="8">${this._renderCurrentStateDetails(t)}${this._renderLatestRun(t)}</td></tr>` : ""}`
    );
  }
  render() {
    return r`
      <uui-box headline="Background Jobs">
        <uui-button slot="header-actions" look="secondary" label="Refresh" @click=${this._reload} .state=${this._reloadState}>
          Refresh
        </uui-button>
        <p>Recurring background jobs registered in Umbraco with status, schedule, and manual trigger.</p>
        <p class="muted refresh-info">Auto-refreshes every ${o._autoRefreshIntervalMs / 1e3} seconds when custom jobs are present.</p>
        ${this._errorMessage ? r`<p class="error">${this._errorMessage}</p>` : ""}
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
                <th>Schedule</th>
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
};
o._autoRefreshIntervalMs = 5e3;
o.styles = [
  f,
  h`
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

      .empty-state {
        display: flex;
        flex-direction: column;
        gap: var(--uui-size-space-2);
        padding: var(--uui-size-space-2) 0;
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
      }

      .details td {
        background: var(--uui-color-surface-alt);
      }

      .persisted-run {
        margin-top: var(--uui-size-space-3);
        display: grid;
        gap: var(--uui-size-space-2);
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
  g("jobs-jobs-jobs-background-jobs-dashboard")
], o);
const x = o;
export {
  o as JobsJobsJobsBackgroundJobsDashboardElement,
  x as default
};
//# sourceMappingURL=background-jobs-dashboard.element-CG9Snt7V.js.map
