import { ApiError, toApiError } from '../api/apiError';
import { PERMISSIONS, type Permission } from './permissions';
import type { SessionUser } from './session';

/** The query key of the signed-in user. Data of null means there is no session. */
export const SESSION_QUERY_KEY = ['session'] as const;

/** SessionDto from GET /api/v1/auth/session and POST /api/v1/auth/sign-in. */
export interface ServerSession {
  readonly userId: string;
  readonly username: string;
  readonly displayName: string;
  readonly department: string | null;
  readonly roles: readonly string[];
  readonly permissions: readonly string[];
}

const KNOWN_PERMISSIONS: ReadonlySet<string> = new Set<string>(PERMISSIONS);

/**
 * Maps the server session to the client session. Permissions the client does not know are dropped,
 * so the menu can only show screens the client has been built for. The server still checks every request.
 */
export function toSessionUser(session: ServerSession): SessionUser {
  return {
    displayName: session.displayName,
    department: session.department ?? 'No department',
    permissions: new Set(session.permissions.filter((p): p is Permission => KNOWN_PERMISSIONS.has(p))),
  };
}

function isServerSession(value: unknown): value is ServerSession {
  if (typeof value !== 'object' || value === null) return false;
  const record = value as Record<string, unknown>;
  return (
    typeof record['userId'] === 'string' &&
    typeof record['displayName'] === 'string' &&
    Array.isArray(record['roles']) &&
    Array.isArray(record['permissions'])
  );
}

async function readSession(response: Response): Promise<SessionUser> {
  const body: unknown = await response.json();
  if (!isServerSession(body)) {
    throw new ApiError(response.status, null, 'The session could not be read.');
  }
  return toSessionUser(body);
}

/** Returns the signed-in user. Throws ApiError with status 401 when there is no session. */
export async function fetchSession(signal?: AbortSignal): Promise<SessionUser> {
  const response = await fetch('/api/v1/auth/session', {
    headers: { Accept: 'application/json' },
    credentials: 'same-origin',
    signal,
  });
  if (!response.ok) {
    throw await toApiError(response);
  }
  return readSession(response);
}

export async function signIn(username: string, password: string): Promise<SessionUser> {
  const response = await fetch('/api/v1/auth/sign-in', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
    credentials: 'same-origin',
    body: JSON.stringify({ username, password }),
  });
  if (!response.ok) {
    throw await toApiError(response);
  }
  return readSession(response);
}

export async function signOut(): Promise<void> {
  const response = await fetch('/api/v1/auth/sign-out', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    credentials: 'same-origin',
    body: '{}',
  });
  if (!response.ok) {
    throw await toApiError(response);
  }
}
