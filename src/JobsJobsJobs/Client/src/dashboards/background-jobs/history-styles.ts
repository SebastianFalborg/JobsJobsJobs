import { css } from "@umbraco-cms/backoffice/external/lit";

export const backgroundJobsHistoryStyles = css`
  :host {
    display: block;
  }

  .muted {
    color: var(--uui-color-text-alt);
    font-size: var(--uui-type-small-size);
  }

  .error {
    color: var(--uui-color-danger);
  }

  .filter-label {
    font-size: var(--uui-type-small-size);
    color: var(--uui-color-text-alt);
  }

  .filter-select {
    padding: var(--uui-size-space-2) var(--uui-size-space-3);
    border: 1px solid var(--uui-color-border);
    border-radius: var(--uui-border-radius);
    background: var(--uui-color-surface);
    color: var(--uui-color-text);
  }

  .history-filters {
    display: flex;
    flex-direction: column;
    gap: var(--uui-size-space-3);
    margin-bottom: var(--uui-size-space-4);
    padding: var(--uui-size-space-4);
    border: 1px solid var(--uui-color-border);
    border-radius: var(--uui-border-radius);
    background: var(--uui-color-surface);
  }

  .history-filter-row {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: var(--uui-size-space-3);
  }

  .history-search-input {
    flex: 1 1 20rem;
    min-width: 12rem;
  }

  .history-status-chips {
    display: flex;
    flex-wrap: wrap;
    gap: var(--uui-size-space-2);
  }

  .history-status-chip {
    padding: var(--uui-size-space-1) var(--uui-size-space-3);
    border: 1px solid var(--uui-color-border);
    border-radius: 999px;
    background: var(--uui-color-surface);
    color: var(--uui-color-text);
    cursor: pointer;
    font-size: var(--uui-type-small-size);
  }

  .history-status-chip-selected {
    background: var(--uui-color-interactive-emphasis);
    color: var(--uui-color-surface);
    border-color: var(--uui-color-interactive-emphasis);
  }

  .history-run-list {
    list-style: none;
    padding: 0;
    margin: 0;
    display: flex;
    flex-direction: column;
    gap: var(--uui-size-space-3);
  }

  .history-run-item {
    padding: var(--uui-size-space-3);
    border: 1px solid var(--uui-color-border);
    border-radius: var(--uui-border-radius);
    background: var(--uui-color-surface);
    display: flex;
    flex-direction: column;
    gap: var(--uui-size-space-2);
  }

  .history-run-main {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: var(--uui-size-space-3);
  }

  .history-run-job {
    font-size: var(--uui-type-default-size);
  }

  .recent-run-message {
    font-size: var(--uui-type-small-size);
  }

  .status-badge {
    padding: var(--uui-size-space-1) var(--uui-size-space-2);
    border-radius: var(--uui-border-radius);
    font-size: var(--uui-type-small-size);
    background: var(--uui-color-surface-alt);
    color: var(--uui-color-text);
  }

  .status-succeeded {
    background: color-mix(in srgb, var(--uui-color-positive) 25%, transparent);
    color: var(--uui-color-positive-contrast);
  }

  .status-failed {
    background: color-mix(in srgb, var(--uui-color-danger) 25%, transparent);
    color: var(--uui-color-danger-contrast);
  }

  .status-running {
    background: color-mix(in srgb, var(--uui-color-interactive-emphasis) 25%, transparent);
    color: var(--uui-color-interactive-emphasis);
  }

  .status-stopped {
    background: color-mix(in srgb, var(--uui-color-warning) 25%, transparent);
    color: var(--uui-color-warning-contrast);
  }

  .status-information {
    background: color-mix(in srgb, var(--uui-color-interactive-emphasis) 15%, transparent);
    color: var(--uui-color-interactive-emphasis);
  }

  .status-warning {
    background: color-mix(in srgb, var(--uui-color-warning) 25%, transparent);
    color: var(--uui-color-warning-contrast);
  }

  .status-error {
    background: color-mix(in srgb, var(--uui-color-danger) 25%, transparent);
    color: var(--uui-color-danger-contrast);
  }

  .persisted-run {
    border-top: 1px solid var(--uui-color-border);
    padding-top: var(--uui-size-space-2);
  }

  .persisted-run-toggle {
    cursor: pointer;
    list-style: none;
    display: flex;
    align-items: center;
    gap: var(--uui-size-space-2);
  }

  .persisted-run-toggle::-webkit-details-marker {
    display: none;
  }

  .persisted-run-heading {
    display: flex;
    align-items: center;
    gap: var(--uui-size-space-2);
  }

  .persisted-run-indicator {
    display: flex;
    align-items: center;
    gap: var(--uui-size-space-1);
  }

  .persisted-run-chevron {
    transition: transform 120ms ease;
  }

  details[open] > .persisted-run-toggle .persisted-run-chevron {
    transform: rotate(90deg);
  }

  .persisted-run-body {
    margin-top: var(--uui-size-space-2);
    display: flex;
    flex-direction: column;
    gap: var(--uui-size-space-2);
  }

  .log-list {
    list-style: none;
    padding: 0;
    margin: 0;
    display: flex;
    flex-direction: column;
    gap: var(--uui-size-space-1);
  }

  .log-item {
    display: flex;
    flex-direction: column;
    padding: var(--uui-size-space-2);
    border-radius: var(--uui-border-radius);
    background: var(--uui-color-surface-alt);
    gap: var(--uui-size-space-1);
  }

  .log-meta {
    display: flex;
    gap: var(--uui-size-space-2);
    align-items: center;
    font-size: var(--uui-type-small-size);
    color: var(--uui-color-text-alt);
  }

  .log-message {
    font-family: var(--uui-font-monospace, monospace);
    font-size: var(--uui-type-small-size);
    white-space: pre-wrap;
    word-break: break-word;
  }

  .empty-state-panel {
    padding: var(--uui-size-space-4);
    border: 1px dashed var(--uui-color-border);
    border-radius: var(--uui-border-radius);
    background: var(--uui-color-surface);
    text-align: center;
  }

  .history-pagination {
    margin-top: var(--uui-size-space-4);
    display: flex;
    justify-content: space-between;
    align-items: center;
    gap: var(--uui-size-space-3);
    flex-wrap: wrap;
  }

  .history-pagination-actions {
    display: flex;
    align-items: center;
    gap: var(--uui-size-space-2);
  }
`;
