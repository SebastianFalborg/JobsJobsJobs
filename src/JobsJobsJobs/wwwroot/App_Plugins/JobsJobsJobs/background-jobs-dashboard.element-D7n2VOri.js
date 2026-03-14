import { html as r, css as g, state as d, customElement as p } from "@umbraco-cms/backoffice/external/lit";
import { UMB_AUTH_CONTEXT as b } from "@umbraco-cms/backoffice/auth";
import { UmbLitElement as f } from "@umbraco-cms/backoffice/lit-element";
import { UmbTextStyles as _ } from "@umbraco-cms/backoffice/style";
var m = Object.defineProperty, v = Object.getOwnPropertyDescriptor, l = (s, t, e, o) => {
  for (var i = o > 1 ? void 0 : o ? v(t, e) : t, n = s.length - 1, u; n >= 0; n--)
    (u = s[n]) && (i = (o ? u(t, e, i) : u(i)) || i);
  return o && i && m(t, e, i), i;
};
let a = class extends f {
  constructor() {
    super(), this._authCredentials = "include", this._items = [], this._isLoading = !1, this._reloadState = void 0, this._runStates = {}, this._errorMessage = "", this._statusFilter = "all", this.consumeContext(b, (s) => {
      const t = s?.getOpenApiConfiguration();
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
      const s = await this._fetch("/umbraco/jobsjobsjobs/api/v1/background-jobs", {
        method: "GET"
      });
      if (!s.ok)
        throw new Error(await this._readProblem(s));
      const t = await s.json();
      this._items = t.items ?? [];
    } catch (s) {
      this._errorMessage = s instanceof Error ? s.message : "Could not load background jobs.";
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
    }, a._autoRefreshIntervalMs);
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
      const t = await this._fetch(`/umbraco/jobsjobsjobs/api/v1/background-jobs/run/${encodeURIComponent(s)}`, {
        method: "POST"
      });
      if (!t.ok)
        throw new Error(await this._readProblem(t));
      this._runStates = { ...this._runStates, [s]: "success" }, await this._load();
    } catch (t) {
      this._runStates = { ...this._runStates, [s]: "failed" }, this._errorMessage = t instanceof Error ? t.message : `Could not run ${s}.`;
    }
  }
  async _fetch(s, t) {
    const e = new Headers(t?.headers);
    e.set("Content-Type", "application/json");
    const o = await this._getAuthToken();
    return o && e.set("Authorization", `Bearer ${o}`), fetch(s, {
      ...t,
      credentials: this._authCredentials,
      headers: e
    });
  }
  async _readProblem(s) {
    try {
      const t = await s.json();
      return t.detail ?? t.title ?? t.message ?? `Request failed with status ${s.status}.`;
    } catch {
      return `Request failed with status ${s.status}.`;
    }
  }
  _formatDate(s) {
    if (!s) return "-";
    const t = new Date(s);
    return Number.isNaN(t.getTime()) ? s : t.toLocaleString();
  }
  _formatTimeSpan(s) {
    return s.startsWith("00:") || s.startsWith("0."), s;
  }
  _formatDuration(s) {
    if (!s) return "-";
    const t = /^(?:(\d+)\.)?(\d{2}):(\d{2}):(\d{2})(?:\.(\d+))?$/.exec(s);
    if (!t)
      return s;
    const e = Number(t[1] ?? "0"), o = Number(t[2]), i = Number(t[3]), n = Number(t[4]), u = t[5] ?? "", h = u ? Math.round(+`0.${u}` * 1e3) : 0;
    if (e > 0 || o > 0 || i > 0) {
      const c = [];
      return e > 0 && c.push(`${e}d`), o > 0 && c.push(`${o}h`), i > 0 && c.push(`${i}m`), n > 0 && c.push(`${n}s`), c.join(" ");
    }
    return n > 0 ? h === 0 ? `${n}s` : `${n}.${h.toString().padStart(3, "0").replace(/0+$/, "")}s` : `${h}ms`;
  }
  _getStatusLabel(s) {
    return s.isRunning ? "Running" : s.lastStatus;
  }
  _getStatusClass(s) {
    return `status-badge status-${this._getStatusLabel(s).toLowerCase()}`;
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
  _getVisibleItems() {
    return this._items.filter((s) => this._matchesFilter(s));
  }
  _onFilterChange(s) {
    this._statusFilter = s.target.value;
  }
  _renderLatestRun(s) {
    const t = s.latestRun;
    return t ? r`
      <div class="persisted-run">
        <div class="persisted-run-summary">
          <strong>Latest stored run:</strong>
          <span>${this._formatDate(t.startedAt)}</span>
          <span>${t.trigger}</span>
          <span>${t.status}</span>
          <span>${this._formatDuration(t.duration)}</span>
        </div>
        ${t.error ? r`<div><strong>Stored error:</strong> ${t.error}</div>` : t.message ? r`<div><strong>Stored message:</strong> ${t.message}</div>` : ""}
        ${t.logs.length > 0 ? r`
              <div class="persisted-run-logs">
                <strong>Stored logs</strong>
                <ul class="log-list">
                  ${t.logs.map(
      (e) => r`
                      <li class="log-item">
                        <span class="log-time">${this._formatDate(e.loggedAt)}</span>
                        <span class="log-level">${e.level}</span>
                        <span class="log-message">${e.message}</span>
                      </li>
                    `
    )}
                </ul>
              </div>
            ` : r`<div class="muted">No stored logs for the latest run.</div>`}
      </div>
    ` : "";
  }
  _renderRows() {
    const s = this._getVisibleItems();
    if (s.length === 0) {
      const t = this._items.length === 0 ? "No recurring background jobs found." : "No jobs match the selected filter.";
      return r`<tr><td colspan="8">${this._isLoading ? "Loading jobs…" : t}</td></tr>`;
    }
    return s.map(
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
          <td>${this._formatTimeSpan(t.period)}</td>
          <td>
            <uui-button
              look="primary"
              label="Run now"
              ?disabled=${t.allowManualTrigger === !1 || t.isRunning}
              .state=${this._runStates[t.alias]}
              @click=${() => this._runJob(t.alias)}>
              Run now
            </uui-button>
          </td>
        </tr>
        ${t.lastError ? r`<tr class="details"><td colspan="8"><strong>Error:</strong> ${t.lastError}${t.lastMessage ? r`<div><strong>Message:</strong> ${t.lastMessage}</div>` : ""}${this._renderLatestRun(t)}</td></tr>` : t.lastMessage ? r`<tr class="details"><td colspan="8"><strong>Message:</strong> ${t.lastMessage}${this._renderLatestRun(t)}</td></tr>` : t.latestRun ? r`<tr class="details"><td colspan="8">${this._renderLatestRun(t)}</td></tr>` : ""}`
    );
  }
  render() {
    return r`
      <uui-box headline="Background Jobs">
        <uui-button slot="header-actions" look="secondary" label="Refresh" @click=${this._reload} .state=${this._reloadState}>
          Refresh
        </uui-button>
        <p>Recurring background jobs registered in Umbraco with status and manual trigger.</p>
        <p class="muted refresh-info">Auto-refreshes every ${a._autoRefreshIntervalMs / 1e3} seconds.</p>
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
};
a._autoRefreshIntervalMs = 5e3;
a.styles = [
  _,
  g`
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

      .persisted-run-summary {
        display: flex;
        flex-wrap: wrap;
        gap: var(--uui-size-space-3);
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

      .log-time,
      .log-level {
        font-size: var(--uui-type-small-size);
        color: var(--uui-color-text-alt);
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
l([
  d()
], a.prototype, "_items", 2);
l([
  d()
], a.prototype, "_isLoading", 2);
l([
  d()
], a.prototype, "_reloadState", 2);
l([
  d()
], a.prototype, "_runStates", 2);
l([
  d()
], a.prototype, "_errorMessage", 2);
l([
  d()
], a.prototype, "_statusFilter", 2);
a = l([
  p("jobs-jobs-jobs-background-jobs-dashboard")
], a);
const R = a;
export {
  a as JobsJobsJobsBackgroundJobsDashboardElement,
  R as default
};
//# sourceMappingURL=background-jobs-dashboard.element-D7n2VOri.js.map
