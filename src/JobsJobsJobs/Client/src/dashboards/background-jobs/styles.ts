import { css } from "@umbraco-cms/backoffice/external/lit";

export const backgroundJobsDashboardStyles = css`
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
`;
