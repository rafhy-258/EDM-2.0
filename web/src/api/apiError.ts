/** A failed call to the EDM API. `code` is the EDM error code (Part 21.3), for example EDM-AUTH-001. */
export class ApiError extends Error {
  constructor(
    readonly status: number,
    readonly code: string | null,
    message: string,
    readonly fieldErrors: Readonly<Record<string, readonly string[]>> = {},
  ) {
    super(message);
    this.name = 'ApiError';
  }
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

/** Reads a problem response (RFC 9457, Part 21.1) into an ApiError. */
export async function toApiError(response: Response): Promise<ApiError> {
  const body: unknown = await response.json().catch(() => null);
  const record = isRecord(body) ? body : {};
  const code = typeof record['code'] === 'string' ? record['code'] : null;
  const message = typeof record['message'] === 'string' ? record['message'] : 'The request could not be completed.';
  const errors = isRecord(record['errors']) ? record['errors'] : {};
  const fieldErrors: Record<string, readonly string[]> = {};
  for (const [field, messages] of Object.entries(errors)) {
    if (Array.isArray(messages)) {
      fieldErrors[field] = messages.filter((m): m is string => typeof m === 'string');
    }
  }
  return new ApiError(response.status, code, message, fieldErrors);
}
