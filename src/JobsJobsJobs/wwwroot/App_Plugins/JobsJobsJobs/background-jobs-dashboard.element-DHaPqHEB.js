import { html as i, css as h, state as d, customElement as c } from "@umbraco-cms/backoffice/external/lit";
import { UMB_AUTH_CONTEXT as _ } from "@umbraco-cms/backoffice/auth";
import { UmbLitElement as b } from "@umbraco-cms/backoffice/lit-element";
import { UmbTextStyles as g } from "@umbraco-cms/backoffice/style";
var p = Object.defineProperty, f = Object.getOwnPropertyDescriptor, n = (t, s, e, a) => {
  for (var o = a > 1 ? void 0 : a ? f(s, e) : s, u = t.length - 1, l; u >= 0; u--)
    (l = t[u]) && (o = (a ? l(s, e, o) : l(o)) || o);
  return a && o && p(s, e, o), o;
};
let r = class extends b {
  constructor() {
    super(), this._authCredentials = "include", this._items = [], this._isLoading = !1, this._reloadState = void 0, this._runStates = {}, this._errorMessage = "", this.consumeContext(_, (t) => {
      const s = t?.getOpenApiConfiguration();
      this._authToken = s?.token, this._authCredentials = s?.credentials ?? "include", this._load();
    });
  }
  async _getAuthToken() {
    return typeof this._authToken == "function" ? this._authToken() : this._authToken;
  }
  async _load() {
    this._isLoading = !0, this._errorMessage = "";
    try {
      const t = await this._fetch("/umbraco/jobsjobsjobs/api/v1/background-jobs", {
        method: "GET"
      });
      if (!t.ok)
        throw new Error(await this._readProblem(t));
      const s = await t.json();
      this._items = s.items ?? [];
    } catch (t) {
      this._errorMessage = t instanceof Error ? t.message : "Could not load background jobs.";
    } finally {
      this._isLoading = !1;
    }
  }
  async _reload() {
    this._reloadState = "waiting", await this._load(), this._reloadState = this._errorMessage ? "failed" : "success";
  }
  async _runJob(t) {
    this._runStates = { ...this._runStates, [t]: "waiting" }, this._errorMessage = "";
    try {
      const s = await this._fetch(`/umbraco/jobsjobsjobs/api/v1/background-jobs/run/${t}`, {
        method: "POST"
      });
      if (!s.ok)
        throw new Error(await this._readProblem(s));
      this._runStates = { ...this._runStates, [t]: "success" }, await this._load();
    } catch (s) {
      this._runStates = { ...this._runStates, [t]: "failed" }, this._errorMessage = s instanceof Error ? s.message : `Could not run ${t}.`;
    }
  }
  async _fetch(t, s) {
    const e = new Headers(s?.headers);
    e.set("Content-Type", "application/json");
    const a = await this._getAuthToken();
    return a && e.set("Authorization", `Bearer ${a}`), fetch(t, {
      ...s,
      credentials: this._authCredentials,
      headers: e
    });
  }
  async _readProblem(t) {
    try {
      const s = await t.json();
      return s.detail ?? s.title ?? s.message ?? `Request failed with status ${t.status}.`;
    } catch {
      return `Request failed with status ${t.status}.`;
    }
  }
  _formatDate(t) {
    if (!t) return "-";
    const s = new Date(t);
    return Number.isNaN(s.getTime()) ? t : s.toLocaleString();
  }
  _formatTimeSpan(t) {
    return t.startsWith("00:") || t.startsWith("0."), t;
  }
  _renderRows() {
    return this._items.length === 0 ? i`<tr><td colspan="7">${this._isLoading ? "Loading jobs…" : "No recurring background jobs found."}</td></tr>` : this._items.map(
      (t) => i`
        <tr>
          <td>
            <strong>${t.name}</strong>
            <div class="muted">${t.alias}</div>
          </td>
          <td>${t.lastStatus}${t.isRunning ? " (running)" : ""}</td>
          <td>${this._formatDate(t.lastSucceededAt)}</td>
          <td>${this._formatDate(t.lastFailedAt)}</td>
          <td>${this._formatDate(t.lastStartedAt)}</td>
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
        ${t.lastError ? i`<tr class="details"><td colspan="7"><strong>Error:</strong> ${t.lastError}</td></tr>` : t.lastMessage ? i`<tr class="details"><td colspan="7"><strong>Message:</strong> ${t.lastMessage}</td></tr>` : ""}`
    );
  }
  render() {
    return i`
      <uui-box headline="Background Jobs">
        <uui-button slot="header-actions" look="secondary" label="Refresh" @click=${this._reload} .state=${this._reloadState}>
          Refresh
        </uui-button>
        <p>Recurring background jobs registered in Umbraco with status and manual trigger.</p>
        ${this._errorMessage ? i`<p class="error">${this._errorMessage}</p>` : ""}
        <div class="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Job</th>
                <th>Status</th>
                <th>Last success</th>
                <th>Last failure</th>
                <th>Last start</th>
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
r.styles = [
  g,
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

      .details td {
        background: var(--uui-color-surface-alt);
      }

      .error {
        color: var(--uui-color-danger);
        font-weight: 700;
      }
    `
];
n([
  d()
], r.prototype, "_items", 2);
n([
  d()
], r.prototype, "_isLoading", 2);
n([
  d()
], r.prototype, "_reloadState", 2);
n([
  d()
], r.prototype, "_runStates", 2);
n([
  d()
], r.prototype, "_errorMessage", 2);
r = n([
  c("jobs-jobs-jobs-background-jobs-dashboard")
], r);
const S = r;
export {
  r as JobsJobsJobsBackgroundJobsDashboardElement,
  S as default
};
//# sourceMappingURL=background-jobs-dashboard.element-DHaPqHEB.js.map
