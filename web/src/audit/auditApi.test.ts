import { afterEach, describe, expect, it, vi } from 'vitest';
import {
  ApiError,
  EMPTY_AUDIT_FILTERS,
  buildAuditQuery,
  fetchAuditEvents,
  pageCountFor,
  validateAuditFilters,
  type AuditEventPage,
} from './auditApi';

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('buildAuditQuery', () => {
  it('sends only paging when no filter is set', () => {
    expect(buildAuditQuery(EMPTY_AUDIT_FILTERS, 1, 25)).toBe('page=1&pageSize=25');
  });

  it('converts the Karachi date range to UTC instants', () => {
    const query = new URLSearchParams(
      buildAuditQuery({ ...EMPTY_AUDIT_FILTERS, fromDate: '2026-11-04', toDate: '2026-11-04' }, 2, 50),
    );
    expect(query.get('from')).toBe('2026-11-03T19:00:00.000Z');
    expect(query.get('to')).toBe('2026-11-04T18:59:59.999Z');
    expect(query.get('page')).toBe('2');
    expect(query.get('pageSize')).toBe('50');
  });

  it('passes the text filters and the outcome, trimmed, and leaves out blanks', () => {
    const query = new URLSearchParams(
      buildAuditQuery(
        { ...EMPTY_AUDIT_FILTERS, module: ' Configuration ', action: '  ', entityType: 'Category', entityId: 'CAT-1', outcome: 'Failure' },
        1,
        25,
      ),
    );
    expect(query.get('module')).toBe('Configuration');
    expect(query.has('action')).toBe(false);
    expect(query.get('entityType')).toBe('Category');
    expect(query.get('entityId')).toBe('CAT-1');
    expect(query.get('outcome')).toBe('Failure');
  });
});

describe('validateAuditFilters', () => {
  it('accepts an open range and a range that ends on its start day', () => {
    expect(validateAuditFilters(EMPTY_AUDIT_FILTERS)).toBeNull();
    expect(validateAuditFilters({ ...EMPTY_AUDIT_FILTERS, fromDate: '2026-11-04', toDate: '2026-11-04' })).toBeNull();
  });

  it('rejects an end date before the start date', () => {
    expect(validateAuditFilters({ ...EMPTY_AUDIT_FILTERS, fromDate: '2026-11-05', toDate: '2026-11-04' })).toMatch(/end date/);
  });
});

describe('pageCountFor', () => {
  it('rounds up and never returns less than one page', () => {
    expect(pageCountFor(0, 25)).toBe(1);
    expect(pageCountFor(25, 25)).toBe(1);
    expect(pageCountFor(26, 25)).toBe(2);
  });
});

const samplePage: AuditEventPage = { items: [], page: 1, pageSize: 25, totalCount: 0 };

describe('fetchAuditEvents', () => {
  it('calls the relative endpoint with the query and returns the page', async () => {
    const fetchMock = vi.fn(async () => new Response(JSON.stringify(samplePage), { status: 200 }));
    vi.stubGlobal('fetch', fetchMock);

    const page = await fetchAuditEvents('page=1&pageSize=25');

    expect(page).toEqual(samplePage);
    expect(fetchMock).toHaveBeenCalledWith(
      '/api/v1/audit-events?page=1&pageSize=25',
      expect.objectContaining({ credentials: 'same-origin' }),
    );
  });

  it('throws an ApiError carrying the status, code and field errors of a problem response', async () => {
    const problem = {
      code: 'EDM-VAL-001',
      message: 'Some inputs are not valid.',
      errors: { pageSize: ['Page size must be between 1 and 200.'], outcome: 'bad' },
    };
    vi.stubGlobal('fetch', async () => new Response(JSON.stringify(problem), { status: 422 }));

    const error = await fetchAuditEvents('pageSize=500').catch((e: unknown) => e);

    expect(error).toBeInstanceOf(ApiError);
    const apiError = error as ApiError;
    expect(apiError.status).toBe(422);
    expect(apiError.code).toBe('EDM-VAL-001');
    expect(apiError.message).toBe('Some inputs are not valid.');
    expect(apiError.fieldErrors).toEqual({ pageSize: ['Page size must be between 1 and 200.'] });
  });

  it('keeps the status when the error body is not JSON', async () => {
    vi.stubGlobal('fetch', async () => new Response('<html>proxy error</html>', { status: 502 }));

    const error = (await fetchAuditEvents('page=1').catch((e: unknown) => e)) as ApiError;

    expect(error).toBeInstanceOf(ApiError);
    expect(error.status).toBe(502);
    expect(error.code).toBeNull();
  });

  it('rejects a 200 response that is not an audit page', async () => {
    vi.stubGlobal('fetch', async () => new Response(JSON.stringify({ unexpected: true }), { status: 200 }));

    const error = (await fetchAuditEvents('page=1').catch((e: unknown) => e)) as ApiError;

    expect(error).toBeInstanceOf(ApiError);
    expect(error.message).toMatch(/could not be read/);
  });
});
