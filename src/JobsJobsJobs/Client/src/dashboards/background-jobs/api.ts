import type { BackgroundJobDashboardCollectionResponseModel } from "./types.js";

const BASE_PATH = "/umbraco/jobsjobsjobs/api/v1/background-jobs";

export interface BackgroundJobsApiOptions {
  getCredentials: () => RequestCredentials;
  getToken: () => Promise<string | undefined>;
  refreshAuth?: () => Promise<boolean>;
}

export class BackgroundJobsApi {
  private readonly _options: BackgroundJobsApiOptions;

  constructor(options: BackgroundJobsApiOptions) {
    this._options = options;
  }

  async list(): Promise<BackgroundJobDashboardCollectionResponseModel> {
    const response = await this._fetch(BASE_PATH, { method: "GET" });

    if (!response.ok) {
      throw new Error(await readProblem(response));
    }

    return (await response.json()) as BackgroundJobDashboardCollectionResponseModel;
  }

  async run(alias: string): Promise<void> {
    const response = await this._fetch(`${BASE_PATH}/run/${encodeURIComponent(alias)}`, { method: "POST" });

    if (!response.ok) {
      throw new Error(await readProblem(response));
    }
  }

  async stop(alias: string): Promise<void> {
    const response = await this._fetch(`${BASE_PATH}/stop/${encodeURIComponent(alias)}`, { method: "POST" });

    if (!response.ok) {
      throw new Error(await readProblem(response));
    }
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
