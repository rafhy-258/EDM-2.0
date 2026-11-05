import { ApiError } from '../api/apiError';

/**
 * Client-side checks for the configuration forms. They mirror the server rules (CatalogueRules and DeptHeadLimitRules in
 * the backend), so the user sees the message before a round trip. The server checks again and remains the authority.
 */
export const NAME_MAX_LENGTH = 200;
export const DESCRIPTION_MAX_LENGTH = 500;

export interface NameDescriptionFields {
  readonly name: string;
  readonly description: string;
}

export type FieldMessages = Readonly<
  Partial<Record<'name' | 'description' | 'categoryId' | 'amountPkr' | 'effectiveDate' | 'nature', string>>
>;

export function validateNameAndDescription(fields: NameDescriptionFields): FieldMessages {
  const messages: Partial<Record<'name' | 'description', string>> = {};
  const name = fields.name.trim();
  if (name.length === 0) {
    messages.name = 'Enter a name.';
  } else if (name.length > NAME_MAX_LENGTH) {
    messages.name = `The name must be ${NAME_MAX_LENGTH} characters or fewer.`;
  }
  if (fields.description.trim().length > DESCRIPTION_MAX_LENGTH) {
    messages.description = `The description must be ${DESCRIPTION_MAX_LENGTH} characters or fewer.`;
  }
  return messages;
}

/** The stored form: trimmed, and null when the description is empty. */
export function normaliseDescription(description: string): string | null {
  const trimmed = description.trim();
  return trimmed.length === 0 ? null : trimmed;
}

/** Field errors returned by the server (ApiError.fieldErrors) as one message per field. */
export function firstFieldMessages(
  fieldErrors: Readonly<Record<string, readonly string[]>>,
): FieldMessages {
  const messages: Partial<Record<'name' | 'description' | 'categoryId' | 'amountPkr' | 'effectiveDate' | 'nature', string>> = {};
  for (const key of ['name', 'description', 'categoryId', 'amountPkr', 'effectiveDate', 'nature'] as const) {
    const first = fieldErrors[key]?.[0];
    if (first) {
      messages[key] = first;
    }
  }
  return messages;
}

/** The message shown after a failed save or delete. Server messages are already user-safe (Part 21.3). */
export function messageFor(error: unknown): string {
  return error instanceof ApiError ? error.message : 'The change could not be saved. Please try again.';
}

/** Today's date in Asia/Karachi as yyyy-MM-dd, the format a date input uses. */
export function karachiToday(now: Date = new Date()): string {
  return new Intl.DateTimeFormat('en-CA', {
    timeZone: 'Asia/Karachi',
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
  }).format(now);
}

export interface DeptHeadLimitFields {
  /** As typed. Parsed only after validation. */
  readonly amount: string;
  readonly description: string;
  /** yyyy-MM-dd. */
  readonly effectiveDate: string;
  /** Today in Karachi, yyyy-MM-dd. Passed in so tests can fix it. */
  readonly today: string;
}

/**
 * Department-head limit form (Master Prompt 5.5). The amount is greater than zero, has at most two decimals, and is
 * required. The effective date is required and cannot be in the past. Mirrors DeptHeadLimitRules on the server.
 */
export function validateDeptHeadLimit(fields: DeptHeadLimitFields): FieldMessages {
  const messages: Partial<Record<'amountPkr' | 'description' | 'effectiveDate', string>> = {};
  const amount = fields.amount.trim();

  if (amount.length === 0) {
    messages.amountPkr = 'Enter an amount.';
  } else if (/^\d+\.\d{3,}$/.test(amount)) {
    messages.amountPkr = 'Use at most two decimal places.';
  } else if (!/^\d+(\.\d{1,2})?$/.test(amount)) {
    messages.amountPkr = 'Enter an amount in PKR, for example 50000 or 50000.50.';
  } else if (Number(amount) <= 0) {
    messages.amountPkr = 'The limit must be greater than zero.';
  }

  if (fields.description.trim().length > DESCRIPTION_MAX_LENGTH) {
    messages.description = `The description must be ${DESCRIPTION_MAX_LENGTH} characters or fewer.`;
  }

  if (fields.effectiveDate.length === 0) {
    messages.effectiveDate = 'Choose an effective date.';
  } else if (fields.effectiveDate < fields.today) {
    messages.effectiveDate = 'The effective date cannot be in the past.';
  }

  return messages;
}

/** The amount as a number. Call only after validateDeptHeadLimit passes. */
export function parseAmount(amount: string): number {
  return Number(amount.trim());
}

/** PKR for display, with thousands separators and two decimals, for example PKR 50,000.00. */
export function formatPkr(amount: number): string {
  return `PKR ${new Intl.NumberFormat('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(amount)}`;
}
