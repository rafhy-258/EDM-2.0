import { afterEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from '../api/apiError';
import {
  createCategory,
  deleteCategory,
  listCategories,
  listSubcategories,
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
