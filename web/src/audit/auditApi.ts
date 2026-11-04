import { ApiError, toApiError } from '../api/apiError';
import { karachiEndOfDayIso, karachiStartOfDayIso } from './format';

export { ApiError };

/** Shapes returned by GET /api/v1/audit-events (backend AuditEventDto and AuditEventPage). */
export type AuditOutcome = 'Success' | 'Failure';

export interface AuditChange {
  readonly field: string;
  readonly before: unknown;
  readonly after: unknown;
}

export interface AuditEvent {
  readonly id: string;
  readonly occurredAtUtc: string;
  readonly actorUserId: string | null;
  readonly actorName: string;
  readonly actorDepartment: string | null;
  readonly actorRoles: readonly string[];
  readonly correlationId: string;
  readonly module: string;
  readonly entityType: string;
  readonly entityId: string;
  readonly action: string;
  readonly outcome: AuditOutcome;
  readonly failureReason: string | null;
  readonly remarks: string | null;
  readonly requestReference: string | null;
  readonly policyVersion: number | null;
  readonly before: unknown;
  readonly after: unknown;
  readonly changes: readonly AuditChange[] | null;
}

export interface AuditEventPage {
  readonly items: readonly AuditEvent[];
  readonly page: number;
  readonly pageSize: number;
  readonly totalCount: number;
}

/** Filter values as the user enters them. Dates are Karachi calendar days (yyyy-mm-dd). */
export interface AuditFilters {
  readonly fromDate: string;
  readonly toDate: string;
  readonly module: string;
  readonly action: string;
  readonly entityType: string;
  readonly entityId: string;
  readonly outcome: '' | AuditOutcome;
}

export const EMPTY_AUDIT_FILTERS: AuditFilters = {
  fromDate: '',
  toDate: '',
  module: '',
  action: '',
  entityType: '',
  entityId: '',
  outcome: '',
};

export const AUDIT_PAGE_SIZE = 25;

/** Error returned by the API, with the EDM error code and any field errors (ADR-0007). */

/**
 * Turns the form state into the query string. Empty fields are left out, so the server applies
 * its defaults. Date-only inputs become UTC instants at Karachi day boundaries.
 */
export function buildAuditQuery(filters: AuditFilters, page: number, pageSize: number): string {
  const params = new URLSearchParams();
  if (filters.fromDate) params.set('from', karachiStartOfDayIso(filters.fromDate));
  if (filters.toDate) params.set('to', karachiEndOfDayIso(filters.toDate));
  if (filters.module.trim()) params.set('module', filters.module.trim());
  if (filters.action.trim()) params.set('action', filters.action.trim());
  if (filters.entityType.trim()) params.set('entityType', filters.entityType.trim());
  if (filters.entityId.trim()) params.set('entityId', filters.entityId.trim());
  if (filters.outcome) params.set('outcome', filters.outcome);
  params.set('page', String(page));
  params.set('pageSize', String(pageSize));
  return params.toString();
}

/** Returns a message when the filters cannot be sent, so the user sees it before the request. */
export function validateAuditFilters(filters: AuditFilters): string | null {
  if (filters.fromDate && filters.toDate && filters.fromDate > filters.toDate) {
    return 'The end date must not be earlier than the start date.';
  }
  return null;
}

export async function fetchAuditEvents(query: string, signal?: AbortSignal): Promise<AuditEventPage> {
  // Relative URL only. The dev server proxies /api to the backend (vite.config.ts).
  const response = await fetch(`/api/v1/audit-events?${query}`, {
    headers: { Accept: 'application/json' },
    credentials: 'same-origin',
    signal,
  });

  if (!response.ok) {
    throw await toApiError(response);
  }

  const body: unknown = await response.json();
  if (!isAuditEventPage(body)) {
    throw new ApiError(response.status, null, 'The audit events could not be read.');
  }
  return body;
}


function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

function isAuditEventPage(value: unknown): value is AuditEventPage {
  return (
    isRecord(value) &&
    Array.isArray(value['items']) &&
    typeof value['page'] === 'number' &&
    typeof value['pageSize'] === 'number' &&
    typeof value['totalCount'] === 'number'
  );
}

export function pageCountFor(totalCount: number, pageSize: number): number {
  return Math.max(1, Math.ceil(totalCount / pageSize));
}
