import { afterEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from '../api/apiError';
import {
  createCategory,
  createCurrency,
  deleteCategory,
  listCategories,
  listSubcategories,
  changeCurrencyRate,
  createRecommendingAuthority,
  listEcMembers,
  setCurrencyActiveState,
  updateCategory,
  type CategoryRecord,
} from './configurationApi';
import { firstFieldMessages, messageFor, normaliseDescription, validateNameAndDescription } from './configurationValidation';

interface Call {
  readonly url: string;
  readonly method: string;
  readonly body: unknown;
}

/** Stubs fetch with one response per call, and records each call for assertions. */
function stubFetch(respond: (call: Call) => Response): Call[] {
  const calls: Call[] = [];
  vi.stubGlobal(
    'fetch',
    vi.fn((url: string, init?: RequestInit) => {
      const call: Call = {
        url,
        method: init?.method ?? 'GET',
        body: init?.body ? JSON.parse(String(init.body)) : undefined,
      };
      calls.push(call);
      return Promise.resolve(respond(call));
    }),
  );
  return calls;
}

function json(status: number, body: unknown): Response {
  return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } });
}

const travel: CategoryRecord = {
  id: '6f1e2a10-0000-4000-8000-000000000001',
  name: 'Travel',
  description: null,
  isActive: true,
  rowVersion: 1,
  subcategoryCount: 0,
  updatedAtUtc: '2026-11-04T05:00:00Z',
  updatedBy: null,
};

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('configuration API client', () => {
  it('reads categories with the inactive flag in the query string', async () => {
    const calls = stubFetch(() => json(200, [travel]));

    const result = await listCategories(true);

    expect(result).toEqual([travel]);
    expect(calls[0]).toEqual({ url: '/api/v1/categories?includeInactive=true', method: 'GET', body: undefined });
  });

  it('sends the row version with an update and returns the saved record', async () => {
    const saved = { ...travel, name: 'Air travel', rowVersion: 2 };
    const calls = stubFetch(() => json(200, saved));

    const result = await updateCategory(travel.id, {
      name: 'Air travel',
      description: null,
      isActive: true,
      rowVersion: 1,
    });

    expect(result).toEqual(saved);
    expect(calls[0]?.method).toBe('PUT');
    expect(calls[0]?.url).toBe(`/api/v1/categories/${travel.id}`);
    expect(calls[0]?.body).toEqual({ name: 'Air travel', description: null, isActive: true, rowVersion: 1 });
  });

  it('turns a field error into an ApiError that carries the field messages', async () => {
    stubFetch(() =>
      json(422, {
        code: 'EDM-VAL-001',
        message: 'Some fields need attention. Please check the highlighted fields.',
        errors: { name: ['A category with this name already exists.'] },
      }),
    );

    const error = await createCategory({ name: 'Travel', description: null }).catch((e: unknown) => e);

    expect(error).toBeInstanceOf(ApiError);
    const apiError = error as ApiError;
    expect(apiError.status).toBe(422);
    expect(apiError.code).toBe('EDM-VAL-001');
    expect(firstFieldMessages(apiError.fieldErrors)).toEqual({ name: 'A category with this name already exists.' });
  });

  it('keeps the concurrency code for a stale update', async () => {
    stubFetch(() =>
      json(412, { code: 'EDM-CONC-001', message: 'This record was changed by someone else. Please reload.' }),
    );

    const error = await updateCategory(travel.id, { name: 'X', description: null, isActive: true, rowVersion: 1 }).catch(
      (e: unknown) => e,
    );

    expect((error as ApiError).code).toBe('EDM-CONC-001');
    expect(messageFor(error)).toBe('This record was changed by someone else. Please reload.');
  });

  it('refuses a success response that does not have the expected shape', async () => {
    stubFetch(() => json(200, { unexpected: true }));

    await expect(listCategories(false)).rejects.toBeInstanceOf(ApiError);
  });

  it('loads the EC pool with filters and validates the member response', async () => {
    const member = {
      id: 'member-1',
      userId: 'user-1',
      employeeId: 'EMP100',
      userName: 'Alex Khan',
      departmentId: 'dept-1',
      departmentName: 'Finance',
      designation: 'EC_MEMBER',
      designationLabel: 'EC Member',
      isActive: true,
      rowVersion: 1,
      updatedAtUtc: '2026-11-04T05:00:00Z',
      updatedBy: null,
    };
    const calls = stubFetch(() => json(200, [member]));

    await expect(listEcMembers('EC_MEMBER', true, ' Alex ')).resolves.toEqual([member]);
    expect(calls[0]?.url).toBe('/api/v1/ec-members?designation=EC_MEMBER&includeInactive=true&search=Alex');
  });

  it('sends an immutable title with the department and user when creating an authority', async () => {
    const authority = {
      id: 'authority-1',
      title: 'Finance Review',
      departmentId: 'dept-1',
      departmentName: 'Finance',
      userId: 'user-1',
      employeeId: 'EMP100',
      userName: 'Alex Khan',
      defaultActionTagSetId: 'tags-1',
      actionTagSetName: 'Recommending Authority',
      isActive: true,
      rowVersion: 1,
      updatedAtUtc: '2026-11-04T05:00:00Z',
      updatedBy: null,
    };
    const calls = stubFetch(() => json(201, authority));

    await expect(createRecommendingAuthority({
      title: 'Finance Review',
      departmentId: 'dept-1',
      userId: 'user-1',
    })).resolves.toEqual(authority);
    expect(calls[0]?.url).toBe('/api/v1/recommending-authorities');
    expect(calls[0]?.body).toEqual({ title: 'Finance Review', departmentId: 'dept-1', userId: 'user-1' });
  });

  it('sends the row version with a delete and accepts no content', async () => {
    const calls = stubFetch(() => new Response(null, { status: 204 }));

    await deleteCategory(travel.id, 3);

    expect(calls[0]).toEqual({ url: `/api/v1/categories/${travel.id}?rowVersion=3`, method: 'DELETE', body: undefined });
  });

  it('filters subcategories by category only when one is chosen', async () => {
    const calls = stubFetch(() => json(200, []));

    await listSubcategories(undefined, false);
    await listSubcategories(travel.id, true);

    expect(calls[0]?.url).toBe('/api/v1/subcategories?includeInactive=false');
    expect(calls[1]?.url).toBe(`/api/v1/subcategories?categoryId=${travel.id}&includeInactive=true`);
  });

  it('sends currency rates as decimal strings and uses the currency endpoints', async () => {
    const currency = {
      id: '00000000-0000-7000-8000-000000000101',
      code: 'USD',
      name: 'US Dollar',
      isBase: false,
      isActive: true,
      rate: '278.1234',
      rowVersion: 1,
      updatedAtUtc: '2026-11-04T05:00:00Z',
      updatedBy: null,
    };
    const rate = {
      id: '00000000-0000-7000-8000-000000000102',
      rate: '280.1234',
      effectiveFrom: '2026-11-05T05:00:00Z',
      effectiveTo: null,
      isActive: true,
      rowVersion: 1,
      updatedAtUtc: '2026-11-05T05:00:00Z',
      updatedBy: null,
    };
    const calls = stubFetch((call) =>
      call.url.endsWith('/rates') ? json(201, rate) : json(201, currency),
    );

    await createCurrency({ code: 'USD', name: 'US Dollar', rate: '278.1234' });
    await changeCurrencyRate(currency.id, '280.1234', 1);

    expect(calls[0]?.url).toBe('/api/v1/currencies');
    expect(calls[0]?.body).toEqual({ code: 'USD', name: 'US Dollar', rate: '278.1234' });
    expect(calls[1]?.url).toBe(`/api/v1/currencies/${currency.id}/rates`);
    expect(calls[1]?.body).toEqual({ rate: '280.1234', rowVersion: 1 });
  });

  it('uses the specified activate and deactivate routes', async () => {
    const currency = {
      id: '00000000-0000-7000-8000-000000000101',
      code: 'USD',
      name: 'US Dollar',
      isBase: false,
      isActive: true,
      rate: '278',
      rowVersion: 2,
      updatedAtUtc: '2026-11-04T05:00:00Z',
      updatedBy: null,
    };
    const calls = stubFetch(() => json(200, currency));

    await setCurrencyActiveState(currency.id, false, 2);

    expect(calls[0]?.url).toBe(`/api/v1/currencies/${currency.id}/deactivate`);
    expect(calls[0]?.body).toEqual({ rowVersion: 2 });
  });
});

describe('configuration validation', () => {
  it('requires a name and limits the lengths the way the server does', () => {
    expect(validateNameAndDescription({ name: '  ', description: '' })).toEqual({ name: 'Enter a name.' });
    expect(validateNameAndDescription({ name: 'a'.repeat(201), description: '' })).toEqual({
      name: 'The name must be 200 characters or fewer.',
    });
    expect(validateNameAndDescription({ name: 'Travel', description: 'd'.repeat(501) })).toEqual({
      description: 'The description must be 500 characters or fewer.',
    });
    expect(validateNameAndDescription({ name: 'Travel', description: '' })).toEqual({});
  });

  it('stores an empty description as null and trims the rest', () => {
    expect(normaliseDescription('   ')).toBeNull();
    expect(normaliseDescription('  Flights  ')).toBe('Flights');
  });
});
