import { ApiError, toApiError } from '../api/apiError';

export { ApiError };

/** Shapes returned by /api/v1/categories and /api/v1/subcategories (backend CategoryResponse, SubcategoryResponse). */
export interface CategoryRecord {
  readonly id: string;
  readonly name: string;
  readonly description: string | null;
  readonly isActive: boolean;
  readonly rowVersion: number;
  readonly subcategoryCount: number;
  readonly updatedAtUtc: string;
  readonly updatedBy: string | null;
}

export interface SubcategoryRecord {
  readonly id: string;
  readonly categoryId: string;
  readonly categoryName: string;
  readonly name: string;
  readonly description: string | null;
  readonly isActive: boolean;
  readonly rowVersion: number;
  readonly updatedAtUtc: string;
  readonly updatedBy: string | null;
}

/** Create a category. The description is null when left empty. */
export interface CategoryCreateInput {
  readonly name: string;
  readonly description: string | null;
}

/** Update a category. rowVersion is the version the user read, so a stale save is refused (EDM-CONC-001). */
export interface CategoryChangeInput extends CategoryCreateInput {
  readonly isActive: boolean;
  readonly rowVersion: number;
}

export interface SubcategoryCreateInput {
  readonly categoryId: string;
  readonly name: string;
  readonly description: string | null;
}

export interface SubcategoryChangeInput {
  readonly name: string;
  readonly description: string | null;
  readonly isActive: boolean;
  readonly rowVersion: number;
}

/** Currency conversion rates are strings to preserve all four supported decimal places in JavaScript. */
export interface CurrencyRecord {
  readonly id: string;
  readonly code: string;
  readonly name: string;
  readonly isBase: boolean;
  readonly isActive: boolean;
  readonly rate: string | null;
  readonly rowVersion: number;
  readonly updatedAtUtc: string;
  readonly updatedBy: string | null;
}

export interface CurrencyRateRecord {
  readonly id: string;
  readonly rate: string;
  readonly effectiveFrom: string;
  readonly effectiveTo: string | null;
  readonly isActive: boolean;
  readonly rowVersion: number;
  readonly updatedAtUtc: string;
  readonly updatedBy: string | null;
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

function isCurrencyRecord(value: unknown): value is CurrencyRecord {
  return (
    isRecord(value) &&
    typeof value['id'] === 'string' &&
    typeof value['code'] === 'string' &&
    typeof value['name'] === 'string' &&
    typeof value['isBase'] === 'boolean' &&
    typeof value['isActive'] === 'boolean' &&
    (typeof value['rate'] === 'string' || value['rate'] === null) &&
    typeof value['rowVersion'] === 'number' &&
    typeof value['updatedAtUtc'] === 'string'
  );
}

function isCurrencyRateRecord(value: unknown): value is CurrencyRateRecord {
  return (
    isRecord(value) &&
    typeof value['id'] === 'string' &&
    typeof value['rate'] === 'string' &&
    typeof value['effectiveFrom'] === 'string' &&
    (typeof value['effectiveTo'] === 'string' || value['effectiveTo'] === null) &&
    typeof value['isActive'] === 'boolean' &&
    typeof value['rowVersion'] === 'number' &&
    typeof value['updatedAtUtc'] === 'string'
  );
}

function isCategoryRecord(value: unknown): value is CategoryRecord {
  return (
    isRecord(value) &&
    typeof value['id'] === 'string' &&
    typeof value['name'] === 'string' &&
    (typeof value['description'] === 'string' || value['description'] === null) &&
    typeof value['isActive'] === 'boolean' &&
    typeof value['rowVersion'] === 'number' &&
    typeof value['subcategoryCount'] === 'number' &&
    typeof value['updatedAtUtc'] === 'string'
  );
}

function isSubcategoryRecord(value: unknown): value is SubcategoryRecord {
  return (
    isRecord(value) &&
    typeof value['id'] === 'string' &&
    typeof value['categoryId'] === 'string' &&
    typeof value['categoryName'] === 'string' &&
    typeof value['name'] === 'string' &&
    (typeof value['description'] === 'string' || value['description'] === null) &&
    typeof value['isActive'] === 'boolean' &&
    typeof value['rowVersion'] === 'number' &&
    typeof value['updatedAtUtc'] === 'string'
  );
}

function unexpectedResponse(status: number): ApiError {
  return new ApiError(status, null, 'The response could not be read. Please try again.');
}

/**
 * Sends a request to the EDM API. Relative URLs only: the dev server proxies /api to the backend (vite.config.ts).
 * Cookies travel with the request (same origin). Problem responses become ApiError with the EDM code.
 */
async function send(method: 'GET' | 'POST' | 'PUT' | 'DELETE', url: string, body?: unknown): Promise<unknown> {
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
  if (response.status === 204) {
    return undefined;
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

export async function listCategories(includeInactive: boolean): Promise<readonly CategoryRecord[]> {
  const body = await send('GET', withQuery('/api/v1/categories', { includeInactive: String(includeInactive) }));
  if (!Array.isArray(body) || !body.every(isCategoryRecord)) {
    throw unexpectedResponse(200);
  }
  return body;
}

export async function createCategory(input: CategoryCreateInput): Promise<CategoryRecord> {
  const body = await send('POST', '/api/v1/categories', input);
  if (!isCategoryRecord(body)) {
    throw unexpectedResponse(201);
  }
  return body;
}

export async function updateCategory(id: string, input: CategoryChangeInput): Promise<CategoryRecord> {
  const body = await send('PUT', `/api/v1/categories/${encodeURIComponent(id)}`, input);
  if (!isCategoryRecord(body)) {
    throw unexpectedResponse(200);
  }
  return body;
}

export async function deleteCategory(id: string, rowVersion: number): Promise<void> {
  await send('DELETE', withQuery(`/api/v1/categories/${encodeURIComponent(id)}`, { rowVersion: String(rowVersion) }));
}

export async function listSubcategories(
  categoryId: string | undefined,
  includeInactive: boolean,
): Promise<readonly SubcategoryRecord[]> {
  const body = await send(
    'GET',
    withQuery('/api/v1/subcategories', { categoryId, includeInactive: String(includeInactive) }),
  );
  if (!Array.isArray(body) || !body.every(isSubcategoryRecord)) {
    throw unexpectedResponse(200);
  }
  return body;
}

export async function createSubcategory(input: SubcategoryCreateInput): Promise<SubcategoryRecord> {
  const body = await send('POST', '/api/v1/subcategories', input);
  if (!isSubcategoryRecord(body)) {
    throw unexpectedResponse(201);
  }
  return body;
}

export async function updateSubcategory(id: string, input: SubcategoryChangeInput): Promise<SubcategoryRecord> {
  const body = await send('PUT', `/api/v1/subcategories/${encodeURIComponent(id)}`, input);
  if (!isSubcategoryRecord(body)) {
    throw unexpectedResponse(200);
  }
  return body;
}

export async function deleteSubcategory(id: string, rowVersion: number): Promise<void> {
  await send('DELETE', withQuery(`/api/v1/subcategories/${encodeURIComponent(id)}`, { rowVersion: String(rowVersion) }));
}

export async function listCurrencies(includeInactive: boolean): Promise<readonly CurrencyRecord[]> {
  const body = await send('GET', withQuery('/api/v1/currencies', { includeInactive: String(includeInactive) }));
  if (!Array.isArray(body) || !body.every(isCurrencyRecord)) {
    throw unexpectedResponse(200);
  }
  return body;
}

export async function createCurrency(input: { readonly code: string; readonly name: string; readonly rate: string }): Promise<CurrencyRecord> {
  const body = await send('POST', '/api/v1/currencies', input);
  if (!isCurrencyRecord(body)) {
    throw unexpectedResponse(201);
  }
  return body;
}

export async function updateCurrency(id: string, name: string, rowVersion: number): Promise<CurrencyRecord> {
  const body = await send('PUT', `/api/v1/currencies/${encodeURIComponent(id)}`, { name, rowVersion });
  if (!isCurrencyRecord(body)) {
    throw unexpectedResponse(200);
  }
  return body;
}

export async function changeCurrencyRate(id: string, rate: string, rowVersion: number): Promise<CurrencyRateRecord> {
  const body = await send('POST', `/api/v1/currencies/${encodeURIComponent(id)}/rates`, { rate, rowVersion });
  if (!isCurrencyRateRecord(body)) {
    throw unexpectedResponse(201);
  }
  return body;
}

export async function listCurrencyRates(id: string): Promise<readonly CurrencyRateRecord[]> {
  const body = await send('GET', `/api/v1/currencies/${encodeURIComponent(id)}/rates`);
  if (!Array.isArray(body) || !body.every(isCurrencyRateRecord)) {
    throw unexpectedResponse(200);
  }
  return body;
}

export async function setCurrencyActiveState(id: string, isActive: boolean, rowVersion: number): Promise<CurrencyRecord> {
  const action = isActive ? 'activate' : 'deactivate';
  const body = await send('POST', `/api/v1/currencies/${encodeURIComponent(id)}/${action}`, { rowVersion });
  if (!isCurrencyRecord(body)) {
    throw unexpectedResponse(200);
  }
  return body;
}

export async function deleteCurrency(id: string, rowVersion: number): Promise<void> {
  await send('DELETE', withQuery(`/api/v1/currencies/${encodeURIComponent(id)}`, { rowVersion: String(rowVersion) }));
}

/** OPEX and CAPEX each have their own department-head limit (Master Prompt 5.5). */
export type LimitNature = 'OPEX' | 'CAPEX';

/** One limit row (backend DeptHeadLimitResponse). Rows are kept as history; the active row has no end date. */
export interface DeptHeadLimitRecord {
  readonly id: string;
  readonly nature: LimitNature;
  readonly amountPkr: number;
  readonly description: string | null;
  readonly effectiveFrom: string;
  readonly effectiveTo: string | null;
  readonly isActive: boolean;
  readonly rowVersion: number;
  readonly updatedAtUtc: string;
  readonly updatedBy: string | null;
}

/** What applies now for one nature (backend DeptHeadLimitSummaryResponse). */
export interface DeptHeadLimitSummary {
  readonly nature: LimitNature;
  readonly currentAmountPkr: number;
  readonly isDefault: boolean;
  readonly status: 'Active' | 'Scheduled' | 'Default';
  readonly defaultAmountPkr: number;
  readonly active: DeptHeadLimitRecord | null;
  readonly lastChangedAtUtc: string | null;
  readonly history: readonly DeptHeadLimitRecord[];
}

/**
 * Set a new limit. effectiveDate is yyyy-MM-dd in Asia/Karachi. rowVersion is the version of the active limit the user
 * read, or null when none is active. A save over a changed limit is refused (EDM-CONC-001).
 */
export interface DeptHeadLimitSaveInput {
  readonly nature: LimitNature;
  readonly amountPkr: number;
  readonly description: string | null;
  readonly effectiveDate: string;
  readonly rowVersion: number | null;
}

function isDeptHeadLimitRecord(value: unknown): value is DeptHeadLimitRecord {
  return (
    isRecord(value) &&
    typeof value['id'] === 'string' &&
    (value['nature'] === 'OPEX' || value['nature'] === 'CAPEX') &&
    typeof value['amountPkr'] === 'number' &&
    (typeof value['description'] === 'string' || value['description'] === null) &&
    typeof value['effectiveFrom'] === 'string' &&
    (typeof value['effectiveTo'] === 'string' || value['effectiveTo'] === null) &&
    typeof value['isActive'] === 'boolean' &&
    typeof value['rowVersion'] === 'number' &&
    typeof value['updatedAtUtc'] === 'string'
  );
}

function isDeptHeadLimitSummary(value: unknown): value is DeptHeadLimitSummary {
  return (
    isRecord(value) &&
    (value['nature'] === 'OPEX' || value['nature'] === 'CAPEX') &&
    typeof value['currentAmountPkr'] === 'number' &&
    typeof value['isDefault'] === 'boolean' &&
    (value['status'] === 'Active' || value['status'] === 'Scheduled' || value['status'] === 'Default') &&
    typeof value['defaultAmountPkr'] === 'number' &&
    (value['active'] === null || isDeptHeadLimitRecord(value['active'])) &&
    (typeof value['lastChangedAtUtc'] === 'string' || value['lastChangedAtUtc'] === null) &&
    Array.isArray(value['history']) &&
    value['history'].every(isDeptHeadLimitRecord)
  );
}

export async function getDeptHeadLimit(nature: LimitNature): Promise<DeptHeadLimitSummary> {
  const body = await send('GET', withQuery('/api/v1/dept-head-limits', { nature }));
  if (!isDeptHeadLimitSummary(body)) {
    throw unexpectedResponse(200);
  }
  return body;
}

export async function saveDeptHeadLimit(input: DeptHeadLimitSaveInput): Promise<DeptHeadLimitRecord> {
  const body = await send('POST', '/api/v1/dept-head-limits', input);
  if (!isDeptHeadLimitRecord(body)) {
    throw unexpectedResponse(201);
  }
  return body;
}

/** Ends the active limit now. The seeded default applies until a new limit is set. */
export async function deactivateDeptHeadLimit(nature: LimitNature, rowVersion: number): Promise<void> {
  await send('POST', '/api/v1/dept-head-limits/deactivate', { nature, rowVersion });
}

export interface ApprovalDirectoryUserRecord {
  readonly id: string;
  readonly employeeId: string;
  readonly name: string;
  readonly departmentId: string;
  readonly departmentName: string;
}

export interface ApprovalDirectoryDepartmentRecord {
  readonly id: string;
  readonly name: string;
}

export interface EcMemberRecord {
  readonly id: string;
  readonly userId: string;
  readonly employeeId: string;
  readonly userName: string;
  readonly departmentId: string;
  readonly departmentName: string;
  readonly designation: string;
  readonly designationLabel: string;
  readonly isActive: boolean;
  readonly rowVersion: number;
  readonly updatedAtUtc: string;
  readonly updatedBy: string | null;
}

export interface RecommendingAuthorityRecord {
  readonly id: string;
  readonly title: string;
  readonly departmentId: string;
  readonly departmentName: string;
  readonly userId: string;
  readonly employeeId: string;
  readonly userName: string;
  readonly defaultActionTagSetId: string;
  readonly actionTagSetName: string;
  readonly isActive: boolean;
  readonly rowVersion: number;
  readonly updatedAtUtc: string;
  readonly updatedBy: string | null;
}

export interface ActionTagSetRecord {
  readonly id: string;
  readonly name: string;
  readonly designation: string;
  readonly designationLabel: string;
  readonly allowedActions: readonly string[];
  readonly isActive: boolean;
  readonly rowVersion: number;
  readonly updatedAtUtc: string;
  readonly updatedBy: string | null;
}

function isApprovalDirectoryUser(value: unknown): value is ApprovalDirectoryUserRecord {
  return isRecord(value) &&
    typeof value['id'] === 'string' &&
    typeof value['employeeId'] === 'string' &&
    typeof value['name'] === 'string' &&
    typeof value['departmentId'] === 'string' &&
    typeof value['departmentName'] === 'string';
}

function isApprovalDirectoryDepartment(value: unknown): value is ApprovalDirectoryDepartmentRecord {
  return isRecord(value) && typeof value['id'] === 'string' && typeof value['name'] === 'string';
}

function isEcMemberRecord(value: unknown): value is EcMemberRecord {
  return isRecord(value) &&
    typeof value['id'] === 'string' &&
    typeof value['userId'] === 'string' &&
    typeof value['employeeId'] === 'string' &&
    typeof value['userName'] === 'string' &&
    typeof value['departmentId'] === 'string' &&
    typeof value['departmentName'] === 'string' &&
    typeof value['designation'] === 'string' &&
    typeof value['designationLabel'] === 'string' &&
    typeof value['isActive'] === 'boolean' &&
    typeof value['rowVersion'] === 'number' &&
    typeof value['updatedAtUtc'] === 'string';
}

function isRecommendingAuthorityRecord(value: unknown): value is RecommendingAuthorityRecord {
  return isRecord(value) &&
    typeof value['id'] === 'string' &&
    typeof value['title'] === 'string' &&
    typeof value['departmentId'] === 'string' &&
    typeof value['departmentName'] === 'string' &&
    typeof value['userId'] === 'string' &&
    typeof value['employeeId'] === 'string' &&
    typeof value['userName'] === 'string' &&
    typeof value['defaultActionTagSetId'] === 'string' &&
    typeof value['actionTagSetName'] === 'string' &&
    typeof value['isActive'] === 'boolean' &&
    typeof value['rowVersion'] === 'number' &&
    typeof value['updatedAtUtc'] === 'string';
}

function isActionTagSetRecord(value: unknown): value is ActionTagSetRecord {
  return isRecord(value) &&
    typeof value['id'] === 'string' &&
    typeof value['name'] === 'string' &&
    typeof value['designation'] === 'string' &&
    typeof value['designationLabel'] === 'string' &&
    Array.isArray(value['allowedActions']) &&
    value['allowedActions'].every((action) => typeof action === 'string') &&
    typeof value['isActive'] === 'boolean' &&
    typeof value['rowVersion'] === 'number' &&
    typeof value['updatedAtUtc'] === 'string';
}

export async function listEcMembers(
  designation: string | undefined,
  includeInactive: boolean,
  search: string,
): Promise<readonly EcMemberRecord[]> {
  const body = await send('GET', withQuery('/api/v1/ec-members', {
    designation,
    includeInactive: String(includeInactive),
    search: search.trim() || undefined,
  }));
  if (!Array.isArray(body) || !body.every(isEcMemberRecord)) throw unexpectedResponse(200);
  return body;
}

export async function listApprovalDirectoryUsers(
  route: 'ec-members' | 'recommending-authorities',
  departmentId?: string,
): Promise<readonly ApprovalDirectoryUserRecord[]> {
  const body = await send('GET', withQuery(`/api/v1/${route}/candidates`, { departmentId }));
  if (!Array.isArray(body) || !body.every(isApprovalDirectoryUser)) throw unexpectedResponse(200);
  return body;
}

export async function listApprovalDepartments(): Promise<readonly ApprovalDirectoryDepartmentRecord[]> {
  const body = await send('GET', '/api/v1/recommending-authorities/departments');
  if (!Array.isArray(body) || !body.every(isApprovalDirectoryDepartment)) throw unexpectedResponse(200);
  return body;
}

export async function createEcMember(input: { readonly userId: string; readonly designation: string }): Promise<EcMemberRecord> {
  const body = await send('POST', '/api/v1/ec-members', input);
  if (!isEcMemberRecord(body)) throw unexpectedResponse(201);
  return body;
}

export async function updateEcMember(id: string, isActive: boolean, rowVersion: number): Promise<EcMemberRecord> {
  const body = await send('PUT', `/api/v1/ec-members/${encodeURIComponent(id)}`, { isActive, rowVersion });
  if (!isEcMemberRecord(body)) throw unexpectedResponse(200);
  return body;
}

export async function deleteEcMember(id: string, rowVersion: number): Promise<void> {
  await send('DELETE', withQuery(`/api/v1/ec-members/${encodeURIComponent(id)}`, { rowVersion: String(rowVersion) }));
}

export async function listRecommendingAuthorities(
  includeInactive: boolean,
  search: string,
): Promise<readonly RecommendingAuthorityRecord[]> {
  const body = await send('GET', withQuery('/api/v1/recommending-authorities', {
    includeInactive: String(includeInactive),
    search: search.trim() || undefined,
  }));
  if (!Array.isArray(body) || !body.every(isRecommendingAuthorityRecord)) throw unexpectedResponse(200);
  return body;
}

export async function createRecommendingAuthority(input: {
  readonly title: string;
  readonly departmentId: string;
  readonly userId: string;
}): Promise<RecommendingAuthorityRecord> {
  const body = await send('POST', '/api/v1/recommending-authorities', input);
  if (!isRecommendingAuthorityRecord(body)) throw unexpectedResponse(201);
  return body;
}

export async function updateRecommendingAuthority(
  id: string,
  input: { readonly departmentId: string; readonly userId: string; readonly isActive: boolean; readonly rowVersion: number },
): Promise<RecommendingAuthorityRecord> {
  const body = await send('PUT', `/api/v1/recommending-authorities/${encodeURIComponent(id)}`, input);
  if (!isRecommendingAuthorityRecord(body)) throw unexpectedResponse(200);
  return body;
}

export async function deleteRecommendingAuthority(id: string, rowVersion: number): Promise<void> {
  await send('DELETE', withQuery(`/api/v1/recommending-authorities/${encodeURIComponent(id)}`, { rowVersion: String(rowVersion) }));
}

export async function listActionTagSets(): Promise<readonly ActionTagSetRecord[]> {
  const body = await send('GET', '/api/v1/action-tag-sets');
  if (!Array.isArray(body) || !body.every(isActionTagSetRecord)) throw unexpectedResponse(200);
  return body;
}

export async function updateActionTagSet(
  id: string,
  allowedActions: readonly string[],
  rowVersion: number,
): Promise<ActionTagSetRecord> {
  const body = await send('PUT', `/api/v1/action-tag-sets/${encodeURIComponent(id)}`, { allowedActions, rowVersion });
  if (!isActionTagSetRecord(body)) throw unexpectedResponse(200);
  return body;
}
