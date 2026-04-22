import type {
  BackgroundJobDashboardCollectionResponseModel,
  BackgroundJobRunHistoryPageResponseModel,
  BackgroundJobRunHistoryQuery,
  BackgroundJobRunLogsResponseModel,
} from "./types.js";

const BASE_PATH = "/umbraco/jobsjobsjobs/api/v1/background-jobs";

export class BackgroundJobsUnauthorizedError extends Error {
  constructor(message: string) {
    super(message);
    this.name = "BackgroundJobsUnauthorizedError";
  }
}

export class BackgroundJobsServerError extends Error {
  readonly status: number;

  constructor(status: number, message: string) {
    super(message);
    this.name = "BackgroundJobsServerError";
    this.status = status;
  }
}

export interface BackgroundJobsApiOptions {
  getCredentials: () => RequestCredentials;
  getToken: () => Promise<string | undefined>;
  refreshAuth?: () => Promise<boolean>;
  onUnauthorized?: () => void;
}

export class BackgroundJobsApi {
  private readonly _options: BackgroundJobsApiOptions;

  constructor(options: BackgroundJobsApiOptions) {
    this._options = options;
  }

  async list(): Promise<BackgroundJobDashboardCollectionResponseModel> {
    const response = await this._fetch(BASE_PATH, { method: "GET" });
    await this._assertOk(response);
    return (await response.json()) as BackgroundJobDashboardCollectionResponseModel;
  }

  async run(alias: string): Promise<void> {
    const response = await this._fetch(`${BASE_PATH}/run/${encodeURIComponent(alias)}`, { method: "POST" });
    await this._assertOk(response);
  }

  async stop(alias: string): Promise<void> {
    const response = await this._fetch(`${BASE_PATH}/stop/${encodeURIComponent(alias)}`, { method: "POST" });
    await this._assertOk(response);
  }

  async getRunLogs(runId: string): Promise<BackgroundJobRunLogsResponseModel> {
    const response = await this._fetch(`${BASE_PATH}/runs/${encodeURIComponent(runId)}/logs`, { method: "GET" });
    await this._assertOk(response);
    return (await response.json()) as BackgroundJobRunLogsResponseModel;
  }

  async queryRuns(query: BackgroundJobRunHistoryQuery): Promise<BackgroundJobRunHistoryPageResponseModel> {
    const params = new URLSearchParams();
    if (query.jobAlias) params.set("jobAlias", query.jobAlias);
    query.statuses?.forEach((status) => params.append("status", status));
    if (query.trigger) params.set("trigger", query.trigger);
    if (query.startedAfter) params.set("startedAfter", query.startedAfter);
    if (query.startedBefore) params.set("startedBefore", query.startedBefore);
    if (query.search) params.set("search", query.search);
    params.set("page", String(query.page));
    params.set("pageSize", String(query.pageSize));

    const response = await this._fetch(`${BASE_PATH}/runs?${params.toString()}`, { method: "GET" });
    await this._assertOk(response);
    return (await response.json()) as BackgroundJobRunHistoryPageResponseModel;
  }

  private async _assertOk(response: Response) {
    if (response.ok) {
      return;
    }

    const message = await readProblem(response);

    if (response.status === 401) {
      this._options.onUnauthorized?.();
      throw new BackgroundJobsUnauthorizedError(message);
    }

    if (response.status >= 500 && response.status <= 599) {
      throw new BackgroundJobsServerError(response.status, message);
    }

    throw new Error(message);
  }

  private async _fetch(input: RequestInfo | URL, init: RequestInit) {
    const response = await this._dispatch(input, init);

    if (response.status !== 401 || this._options.refreshAuth === undefined) {
      return response;
    }

    const refreshed = await this._options.refreshAuth();
    if (refreshed === false) {
      return response;
    }

    return this._dispatch(input, init);
  }

  private async _dispatch(input: RequestInfo | URL, init: RequestInit) {
    const token = await this._options.getToken();
    if (!token) {
      throw new Error("Backoffice authentication is not ready yet.");
    }

    const headers = new Headers(init.headers);
    headers.set("Content-Type", "application/json");
    headers.set("Authorization", `Bearer ${token}`);

    return fetch(input, {
      ...init,
      credentials: this._options.getCredentials(),
      headers,
    });
  }
}

async function readProblem(response: Response): Promise<string> {
  try {
    const problem = (await response.json()) as { detail?: string; title?: string; message?: string };
    return problem.detail ?? problem.title ?? problem.message ?? `Request failed with status ${response.status}.`;
  } catch {
    return `Request failed with status ${response.status}.`;
  }
}
