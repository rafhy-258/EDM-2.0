/**
 * One colour and one label per status across the product (Master Prompt 8.1, 14.2).
 * The label carries the meaning. Colour is never the only signal (WCAG 2.2 AA).
 */
export type BadgeColor =
  | 'brand'
  | 'success'
  | 'warning'
  | 'danger'
  | 'informative'
  | 'subtle';

export interface StatusDefinition {
  readonly label: string;
  readonly color: BadgeColor;
  /** Phase in which the status first appears in the product. */
  readonly phase: 'P1' | 'P2';
}

/** Request status (Part 8.1). Draft is a Proposed Enhancement and therefore Phase 2. */
export const REQUEST_STATUS = {
  DRAFT: { label: 'Draft', color: 'subtle', phase: 'P2' },
  PENDING: { label: 'Pending', color: 'informative', phase: 'P1' },
  REVERTED: { label: 'Reverted', color: 'warning', phase: 'P1' },
  APPROVED: { label: 'Approved', color: 'success', phase: 'P1' },
  REJECTED: { label: 'Rejected', color: 'danger', phase: 'P1' },
} as const satisfies Record<string, StatusDefinition>;

/** Step state (Part 8.1, 22.3). */
export const STEP_STATE = {
  WAITING: { label: 'Waiting', color: 'subtle', phase: 'P1' },
  ACTIVE: { label: 'Active', color: 'brand', phase: 'P1' },
  APPROVED: { label: 'Approved', color: 'success', phase: 'P1' },
  REJECTED: { label: 'Rejected', color: 'danger', phase: 'P1' },
  REVERTED: { label: 'Reverted', color: 'warning', phase: 'P1' },
  FORWARDED: { label: 'Forwarded', color: 'informative', phase: 'P1' },
  CLOSED: { label: 'Closed (Quorum Met)', color: 'subtle', phase: 'P1' },
  SUPERSEDED: { label: 'Superseded', color: 'subtle', phase: 'P1' },
} as const satisfies Record<string, StatusDefinition>;

export type RequestStatusKey = keyof typeof REQUEST_STATUS;
export type StepStateKey = keyof typeof STEP_STATE;
export type StatusKey = RequestStatusKey | StepStateKey;

/** Lookup across both families. Keys are unique, so there is no ambiguity. */
export function statusDefinition(key: StatusKey): StatusDefinition {
  if (key in REQUEST_STATUS) {
    return REQUEST_STATUS[key as RequestStatusKey];
  }
  return STEP_STATE[key as StepStateKey];
}
