import { customElement, html, property, state } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { BackgroundJobsServerError, BackgroundJobsUnauthorizedError } from "./background-jobs/api.js";
import type { BackgroundJobsApi } from "./background-jobs/api.js";
import { formatDate, formatDuration, getStatusClassFromValue } from "./background-jobs/formatting.js";
import { backgroundJobsHistoryStyles } from "./background-jobs/history-styles.js";
import type {
  BackgroundJobDashboardItem,
  BackgroundJobRunHistoryItem,
  BackgroundJobRunHistoryQuery,
  BackgroundJobRunLogEntry,
  BackgroundJobRunStatus,
  BackgroundJobRunTriggerFilter,
} from "./background-jobs/types.js";

interface RunLogsState {
  loading: boolean;
  loaded?: boolean;
  logs?: Array<BackgroundJobRunLogEntry>;
  error?: string;
}

const STATUS_OPTIONS: Array<BackgroundJobRunStatus> = ["Running", "Succeeded", "Failed", "Stopped", "Ignored", "Idle"];

const PAGE_SIZE_OPTIONS = [10, 25, 50];

const SEARCH_DEBOUNCE_MS = 300;

@customElement("jobs-jobs-jobs-background-jobs-history")
export class JobsJobsJobsBackgroundJobsHistoryElement extends UmbLitElement {
  @property({ attribute: false })
  api?: BackgroundJobsApi;

  @property({ attribute: false })
  jobs: Array<BackgroundJobDashboardItem> = [];

  @state()
  private _filterJobAlias = "";

  @state()
  private _filterStatuses: Array<BackgroundJobRunStatus> = [];

  @state()
  private _filterTrigger: BackgroundJobRunTriggerFilter | "" = "";

  @state()
  private _filterStartedAfter = "";

  @state()
  private _filterStartedBefore = "";

  @state()
  private _filterSearch = "";

  @state()
  private _page = 1;

  @state()
  private _pageSize = 25;

  @state()
  private _items: Array<BackgroundJobRunHistoryItem> = [];

  @state()
  private _total = 0;

  @state()
  private _isLoading = false;

  @state()
  private _errorMessage = "";

  @state()
  private _runLogsState: Record<string, RunLogsState> = {};

  private _searchDebounceHandle?: number;

  private _hasLoadedOnce = false;

  setPreFilterAlias(alias: string) {
    this._filterJobAlias = alias;
    this._page = 1;
    void this._load();
  }

  override connectedCallback() {
    super.connectedCallback();
    if (this._hasLoadedOnce === false) {
      void this._load();
    }
  }

  override disconnectedCallback() {
    if (this._searchDebounceHandle !== undefined) {
      window.clearTimeout(this._searchDebounceHandle);
      this._searchDebounceHandle = undefined;
    }
    super.disconnectedCallback();
  }

  private async _load() {
    if (this.api === undefined) {
      return;
    }

    this._isLoading = true;
    this._errorMessage = "";

    const query: BackgroundJobRunHistoryQuery = {
      jobAlias: this._filterJobAlias || undefined,
      statuses: this._filterStatuses.length > 0 ? this._filterStatuses : undefined,
      trigger: this._filterTrigger || undefined,
      startedAfter: this._filterStartedAfter ? new Date(this._filterStartedAfter).toISOString() : undefined,
      startedBefore: this._filterStartedBefore ? new Date(this._filterStartedBefore).toISOString() : undefined,
      search: this._filterSearch ? this._filterSearch.trim() : undefined,
      page: this._page,
      pageSize: this._pageSize,
    };

    try {
      const result = await this.api.queryRuns(query);
      this._items = result.items ?? [];
      this._total = result.total ?? 0;
      this._hasLoadedOnce = true;
    } catch (error) {
      if (error instanceof BackgroundJobsUnauthorizedError) {
        this._errorMessage = "Your session has expired. Please sign in again.";
      } else if (error instanceof BackgroundJobsServerError) {
        this._errorMessage = `The server returned an error (status ${error.status}). Click Refresh to try again.`;
      } else {
        this._errorMessage = error instanceof Error ? error.message : "Could not load run history.";
      }
      this._items = [];
      this._total = 0;
    } finally {
      this._isLoading = false;
    }
  }

  private _refresh = () => {
    void this._load();
  };

  private _onJobAliasChange = (event: Event) => {
    this._filterJobAlias = (event.target as HTMLSelectElement).value;
    this._page = 1;
    void this._load();
  };

  private _onStatusToggle = (status: BackgroundJobRunStatus) => () => {
    this._filterStatuses = this._filterStatuses.includes(status)
      ? this._filterStatuses.filter((value) => value !== status)
      : [...this._filterStatuses, status];
    this._page = 1;
    void this._load();
  };

  private _onTriggerChange = (event: Event) => {
    this._filterTrigger = (event.target as HTMLSelectElement).value as BackgroundJobRunTriggerFilter | "";
    this._page = 1;
    void this._load();
  };

  private _onStartedAfterChange = (event: Event) => {
    this._filterStartedAfter = (event.target as HTMLInputElement).value;
    this._page = 1;
    void this._load();
  };

  private _onStartedBeforeChange = (event: Event) => {
    this._filterStartedBefore = (event.target as HTMLInputElement).value;
    this._page = 1;
    void this._load();
  };

  private _onSearchInput = (event: Event) => {
    this._filterSearch = (event.target as HTMLInputElement).value;
    if (this._searchDebounceHandle !== undefined) {
      window.clearTimeout(this._searchDebounceHandle);
    }
    this._searchDebounceHandle = window.setTimeout(() => {
      this._page = 1;
      void this._load();
    }, SEARCH_DEBOUNCE_MS);
  };

  private _onPageSizeChange = (event: Event) => {
    this._pageSize = Number((event.target as HTMLSelectElement).value);
    this._page = 1;
    void this._load();
  };

  private _clearFilters = () => {
    this._filterJobAlias = "";
    this._filterStatuses = [];
    this._filterTrigger = "";
    this._filterStartedAfter = "";
    this._filterStartedBefore = "";
    this._filterSearch = "";
    this._page = 1;
    void this._load();
  };

  private _goToPage(page: number) {
    this._page = page;
    void this._load();
  }

  private _totalPages(): number {
    return Math.max(1, Math.ceil(this._total / this._pageSize));
  }

  private async _loadRunLogs(runId: string) {
    if (this.api === undefined) {
      return;
    }

    const existing = this._runLogsState[runId];
    if (existing?.loading || existing?.loaded) {
      return;
    }

    this._runLogsState = { ...this._runLogsState, [runId]: { loading: true } };

    try {
      const result = await this.api.getRunLogs(runId);
      this._runLogsState = {
        ...this._runLogsState,
        [runId]: { loading: false, loaded: true, logs: result.logs ?? [] },
      };
    } catch (error) {
      const message =
        error instanceof BackgroundJobsUnauthorizedError
          ? "Your session has expired. Please sign in again."
          : error instanceof Error
            ? error.message
            : "Could not load run logs.";
      this._runLogsState = {
        ...this._runLogsState,
        [runId]: { loading: false, loaded: false, error: message },
      };
    }
  }

  private _onRunLogsToggle = (runId: string) => (event: Event) => {
    const details = event.currentTarget as HTMLDetailsElement;
    if (details.open) {
      void this._loadRunLogs(runId);
    }
  };

  private _renderFilters() {
    const jobOptions = [...this.jobs].sort((a, b) => a.name.localeCompare(b.name));

    return html`
      <div class="history-filters">
        <div class="history-filter-row">
          <label class="filter-label" for="history-job">Job</label>
          <select id="history-job" class="filter-select" @change=${this._onJobAliasChange} .value=${this._filterJobAlias}>
            <option value="">All jobs</option>
            ${jobOptions.map((job) => html`<option value=${job.alias}>${job.name}</option>`)}
          </select>

          <label class="filter-label" for="history-trigger">Trigger</label>
          <select id="history-trigger" class="filter-select" @change=${this._onTriggerChange} .value=${this._filterTrigger}>
            <option value="">Any</option>
            <option value="Automatic">Automatic</option>
            <option value="Manual">Manual</option>
          </select>
        </div>

        <div class="history-filter-row">
          <label class="filter-label">Status</label>
          <div class="history-status-chips">
            ${STATUS_OPTIONS.map((status) => {
              const selected = this._filterStatuses.includes(status);
              return html`
                <button
                  type="button"
                  class="history-status-chip ${selected ? "history-status-chip-selected" : ""}"
                  @click=${this._onStatusToggle(status)}>
                  ${status}
                </button>
              `;
            })}
          </div>
        </div>

        <div class="history-filter-row">
          <label class="filter-label" for="history-started-after">Started after</label>
          <input
            id="history-started-after"
            class="filter-select"
            type="datetime-local"
            .value=${this._filterStartedAfter}
            @change=${this._onStartedAfterChange} />

          <label class="filter-label" for="history-started-before">Started before</label>
          <input
            id="history-started-before"
            class="filter-select"
            type="datetime-local"
            .value=${this._filterStartedBefore}
            @change=${this._onStartedBeforeChange} />
        </div>

        <div class="history-filter-row">
          <label class="filter-label" for="history-search">Search</label>
          <input
            id="history-search"
            class="filter-select history-search-input"
            type="search"
            placeholder="Search run errors, messages, and log lines…"
            .value=${this._filterSearch}
            @input=${this._onSearchInput} />

          <uui-button look="secondary" label="Clear filters" @click=${this._clearFilters}>Clear filters</uui-button>
          <uui-button look="primary" label="Refresh" @click=${this._refresh}>Refresh</uui-button>
        </div>
      </div>
    `;
  }

  private _renderPagination() {
    const totalPages = this._totalPages();
    const hasPrev = this._page > 1;
    const hasNext = this._page < totalPages;

    return html`
      <div class="history-pagination">
        <div class="muted">${this._total} runs · Page ${this._page} of ${totalPages}</div>
        <div class="history-pagination-actions">
          <label class="filter-label" for="history-page-size">Page size</label>
          <select id="history-page-size" class="filter-select" @change=${this._onPageSizeChange} .value=${String(this._pageSize)}>
            ${PAGE_SIZE_OPTIONS.map((size) => html`<option value=${String(size)}>${size}</option>`)}
          </select>
          <uui-button look="secondary" label="Previous" ?disabled=${!hasPrev} @click=${() => this._goToPage(this._page - 1)}>
            Previous
          </uui-button>
          <uui-button look="secondary" label="Next" ?disabled=${!hasNext} @click=${() => this._goToPage(this._page + 1)}>
            Next
          </uui-button>
        </div>
      </div>
    `;
  }

  private _renderRunLogs(runId: string) {
    const state = this._runLogsState[runId];

    if (state?.loading) {
      return html`<div class="muted">Loading logs…</div>`;
    }

    if (state?.error) {
      return html`<div class="error">${state.error}</div>`;
    }

    const logs = state?.logs ?? [];

    if (state?.loaded && logs.length === 0) {
      return html`<div class="muted">No log lines for this run.</div>`;
    }

    if (logs.length === 0) {
      return "";
    }

    return html`
      <ul class="log-list">
        ${logs.map(
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
    `;
  }

  private _renderRunItem(run: BackgroundJobRunHistoryItem) {
    return html`
      <li class="recent-run-item history-run-item">
        <div class="recent-run-main history-run-main">
          <span class=${getStatusClassFromValue(run.status)}>${run.status}</span>
          <strong class="history-run-job">${run.jobName}</strong>
          <span class="muted">${formatDate(run.startedAt)}</span>
          <span class="muted">${run.trigger}</span>
          <span class="muted">${formatDuration(run.duration)}</span>
        </div>
        ${run.error
          ? html`<div class="recent-run-message"><strong>Error:</strong> ${run.error}</div>`
          : run.message
            ? html`<div class="recent-run-message"><strong>Message:</strong> ${run.message}</div>`
            : ""}
        <details class="persisted-run" @toggle=${this._onRunLogsToggle(run.id)}>
          <summary class="persisted-run-toggle">
            <span class="persisted-run-heading">
              <span class="persisted-run-indicator" aria-hidden="true">
                <span class="persisted-run-chevron">▸</span>
                <span class="muted">Show logs</span>
              </span>
            </span>
          </summary>
          <div class="persisted-run-body">${this._renderRunLogs(run.id)}</div>
        </details>
      </li>
    `;
  }

  private _renderList() {
    if (this._isLoading && this._items.length === 0) {
      return html`<div class="empty-state-panel">Loading runs…</div>`;
    }

    if (this._items.length === 0) {
      return html`<div class="empty-state-panel">No runs match the current filters.</div>`;
    }

    return html`<ul class="recent-run-list history-run-list">${this._items.map((run) => this._renderRunItem(run))}</ul>`;
  }

  override render() {
    return html`
      ${this._renderFilters()}
      ${this._errorMessage ? html`<p class="error">${this._errorMessage}</p>` : ""}
      ${this._renderList()}
      ${this._renderPagination()}
    `;
  }

  static override styles = [backgroundJobsHistoryStyles];
}

export default JobsJobsJobsBackgroundJobsHistoryElement;

declare global {
  interface HTMLElementTagNameMap {
    "jobs-jobs-jobs-background-jobs-history": JobsJobsJobsBackgroundJobsHistoryElement;
  }
}
