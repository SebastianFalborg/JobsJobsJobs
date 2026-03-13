import { css, customElement, html, state } from "@umbraco-cms/backoffice/external/lit";
import { UMB_AUTH_CONTEXT } from "@umbraco-cms/backoffice/auth";
import type { UUIButtonState } from "@umbraco-cms/backoffice/external/uui";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";

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
  lastSucceededAt?: string;
  lastFailedAt?: string;
  lastStatus: string;
  lastError?: string;
  lastMessage?: string;
}

interface BackgroundJobDashboardCollectionResponseModel {
  total: number;
  items: Array<BackgroundJobDashboardItem>;
}

@customElement("jobs-jobs-jobs-background-jobs-dashboard")
export class JobsJobsJobsBackgroundJobsDashboardElement extends UmbLitElement {
  private _authToken?: string | (() => Promise<string>);

  private _authCredentials: RequestCredentials = "include";

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

  constructor() {
    super();

    this.consumeContext(UMB_AUTH_CONTEXT, (authContext) => {
      const config = authContext?.getOpenApiConfiguration();
      this._authToken = config?.token;
      this._authCredentials = config?.credentials ?? "include";
      this._load();
    });
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

  private async _runJob(alias: string) {
    this._runStates = { ...this._runStates, [alias]: "waiting" };
    this._errorMessage = "";

    try {
      const response = await this._fetch(`/umbraco/jobsjobsjobs/api/v1/background-jobs/run/${alias}`, {
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

  private _renderRows() {
    if (this._items.length === 0) {
      return html`<tr><td colspan="7">${this._isLoading ? "Loading jobs…" : "No recurring background jobs found."}</td></tr>`;
    }

    return this._items.map(
      (item) => html`
        <tr>
          <td>
            <strong>${item.name}</strong>
            <div class="muted">${item.alias}</div>
          </td>
          <td>${item.lastStatus}${item.isRunning ? " (running)" : ""}</td>
          <td>${this._formatDate(item.lastSucceededAt)}</td>
          <td>${this._formatDate(item.lastFailedAt)}</td>
          <td>${this._formatDate(item.lastStartedAt)}</td>
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
          ? html`<tr class="details"><td colspan="7"><strong>Error:</strong> ${item.lastError}</td></tr>`
          : item.lastMessage
            ? html`<tr class="details"><td colspan="7"><strong>Message:</strong> ${item.lastMessage}</td></tr>`
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
        ${this._errorMessage ? html`<p class="error">${this._errorMessage}</p>` : ""}
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
    `,
  ];
}

export default JobsJobsJobsBackgroundJobsDashboardElement;

declare global {
  interface HTMLElementTagNameMap {
    "jobs-jobs-jobs-background-jobs-dashboard": JobsJobsJobsBackgroundJobsDashboardElement;
  }
}
