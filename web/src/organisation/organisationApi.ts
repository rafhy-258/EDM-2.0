import { ApiError, toApiError } from '../api/apiError';

export { ApiError };

/** A department (backend DepartmentResponse). Departments are deactivated, not deleted. */
export interface DepartmentRecord {
  readonly id: string;
  readonly name: string;
  readonly isActive: boolean;
  readonly rowVersion: number;
  readonly updatedAtUtc: string;
  readonly updatedBy: string | null;
}

/** An active department for pickers (backend DepartmentOption). */
export interface DepartmentOption {
  readonly id: string;
  readonly name: string;
}

/** A person in the organisation directory (backend DirectoryUserResponse). */
export interface DirectoryUserRecord {
  readonly id: string;
  readonly employeeId: string;
  readonly name: string;
  readonly email: string | null;
  readonly departmentId: string;
  readonly isActive: boolean;
  readonly rowVersion: number;
  readonly updatedAtUtc: string;
  readonly updatedBy: string | null;
}

export interface DepartmentCreateInput {
  readonly name: string;
}

/** Rename, deactivate or reactivate. rowVersion is the version the user read (a stale save returns EDM-CONC-001). */
export interface DepartmentChangeInput {
  readonly name: string;
  readonly isActive: boolean;
  readonly rowVersion: number;
}

export interface DirectoryUserCreateInput {
  readonly employeeId: string;
  readonly name: string;
  readonly email: string | null;
  readonly departmentId: string;
}

export interface DirectoryUserChangeInput extends DirectoryUserCreateInput {
  readonly isActive: boolean;
  readonly rowVersion: number;
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

function isDepartmentRecord(value: unknown): value is DepartmentRecord {
  return (
    isRecord(value) &&
    typeof value['id'] === 'string' &&
    typeof value['name'] === 'string' &&
    typeof value['isActive'] === 'boolean' &&
    typeof value['rowVersion'] === 'number' &&
    typeof value['updatedAtUtc'] === 'string'
  );
}

function isDepartmentOption(value: unknown): value is DepartmentOption {
  return isRecord(value) && typeof value['id'] === 'string' && typeof value['name'] === 'string';
}

function isDirectoryUserRecord(value: unknown): value is DirectoryUserRecord {
  return (
    isRecord(value) &&
    typeof value['id'] === 'string' &&
    typeof value['employeeId'] === 'string' &&
    typeof value['name'] === 'string' &&
    (typeof value['email'] === 'string' || value['email'] === null) &&
    typeof value['departmentId'] === 'string' &&
    typeof value['isActive'] === 'boolean' &&
    typeof value['rowVersion'] === 'number' &&
    typeof value['updatedAtUtc'] === 'string'
  );
}

function unexpectedResponse(status: number): ApiError {
  return new ApiError(status, null, 'The response could not be read. Please try again.');
}

/** Relative URLs only: the dev server proxies /api to the backend (vite.config.ts). Cookies travel with the request. */
async function send(method: 'GET' | 'POST' | 'PUT', url: string, body?: unknown): Promise<unknown> {
  const headers: Record<string, string> = { Accept: 'application/json' };
  if (body !== undefined) {
    headers['Content-Type'] = 'application/json';
  }

  const response = await fetch(url, {
    method,
    headers,
    credentials: 'same-origin',
    body: body === undefined ? undefined : JSON.stringify(body),
  });

  if (!response.ok) {
    throw await toApiError(response);
  }
  return response.json().catch(() => {
    throw unexpectedResponse(response.status);
  });
}

function withQuery(path: string, params: Record<string, string | undefined>): string {
  const search = new URLSearchParams();
  for (const [key, value] of Object.entries(params)) {
    if (value !== undefined) {
      search.set(key, value);
    }
  }
  const query = search.toString();
  return query ? `${path}?${query}` : path;
}

export async function listDepartments(includeInactive: boolean): Promise<readonly DepartmentRecord[]> {
  const body = await send('GET', withQuery('/api/v1/departments', { includeInactive: String(includeInactive) }));
  if (!Array.isArray(body) || !body.every(isDepartmentRecord)) {
    throw unexpectedResponse(200);
  }
  return body;
}

export async function listDepartmentOptions(): Promise<readonly DepartmentOption[]> {
  const body = await send('GET', '/api/v1/departments/options');
  if (!Array.isArray(body) || !body.every(isDepartmentOption)) {
    throw unexpectedResponse(200);
  }
  return body;
}

export async function createDepartment(input: DepartmentCreateInput): Promise<DepartmentRecord> {
  const body = await send('POST', '/api/v1/departments', input);
  if (!isDepartmentRecord(body)) {
    throw unexpectedResponse(201);
  }
  return body;
}

export async function updateDepartment(id: string, input: DepartmentChangeInput): Promise<DepartmentRecord> {
  const body = await send('PUT', `/api/v1/departments/${encodeURIComponent(id)}`, input);
  if (!isDepartmentRecord(body)) {
    throw unexpectedResponse(200);
  }
  return body;
}

export async function listDirectoryUsers(
  departmentId: string | undefined,
  includeInactive: boolean,
): Promise<readonly DirectoryUserRecord[]> {
  const body = await send(
    'GET',
    withQuery('/api/v1/users', { departmentId, includeInactive: String(includeInactive) }),
  );
  if (!Array.isArray(body) || !body.every(isDirectoryUserRecord)) {
    throw unexpectedResponse(200);
  }
  return body;
}

export async function createDirectoryUser(input: DirectoryUserCreateInput): Promise<DirectoryUserRecord> {
  const body = await send('POST', '/api/v1/users', input);
  if (!isDirectoryUserRecord(body)) {
    throw unexpectedResponse(201);
  }
  return body;
}

export async function updateDirectoryUser(id: string, input: DirectoryUserChangeInput): Promise<DirectoryUserRecord> {
  const body = await send('PUT', `/api/v1/users/${encodeURIComponent(id)}`, input);
  if (!isDirectoryUserRecord(body)) {
    throw unexpectedResponse(200);
  }
  return body;
}
