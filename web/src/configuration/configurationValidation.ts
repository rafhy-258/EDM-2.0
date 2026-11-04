import { ApiError } from '../api/apiError';

/**
 * Client-side checks for the configuration forms. They mirror the server rules (CatalogueRules in the backend), so the
 * user sees the message before a round trip. The server checks again and remains the authority.
 */
export const NAME_MAX_LENGTH = 200;
export const DESCRIPTION_MAX_LENGTH = 500;

export interface NameDescriptionFields {
  readonly name: string;
  readonly description: string;
}

export type FieldMessages = Readonly<Partial<Record<'name' | 'description' | 'categoryId', string>>>;

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
  const messages: Partial<Record<'name' | 'description' | 'categoryId', string>> = {};
  for (const key of ['name', 'description', 'categoryId'] as const) {
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
