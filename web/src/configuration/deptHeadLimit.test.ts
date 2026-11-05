import { afterEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from '../api/apiError';
import { deactivateDeptHeadLimit, getDeptHeadLimit, saveDeptHeadLimit } from './configurationApi';
import {
  formatPkr,
  karachiToday,
  parseAmount,
  validateDeptHeadLimit,
} from './configurationValidation';

interface Call {
  readonly url: string;
  readonly method: string;
  readonly body: unknown;
}

function stubFetch(respond: (call: Call) => Response): Call[] {
  const calls: Call[] = [];
  vi.stubGlobal(
    'fetch',
    vi.fn((url: string, init?: RequestInit) => {
      const call: Call = { url, method: init?.method ?? 'GET', body: init?.body ? JSON.parse(String(init.body)) : undefined };
      calls.push(call);
      return Promise.resolve(respond(call));
    }),
  );
  return calls;
}

function json(status: number, body: unknown): Response {
  return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } });
}

const record = {
  id: '6f1e2a10-0000-4000-8000-000000000010',
  nature: 'OPEX',
  amountPkr: 50000,
  description: null,
  effectiveFrom: '2026-11-04T19:00:00Z',
  effectiveTo: null,
  isActive: true,
  rowVersion: 1,
  updatedAtUtc: '2026-11-04T05:00:00Z',
  updatedBy: null,
};

const summary = {
  nature: 'OPEX',
  currentAmountPkr: 50000,
  isDefault: false,
  status: 'Active',
  defaultAmountPkr: 20000,
  active: record,
  lastChangedAtUtc: record.updatedAtUtc,
  history: [record],
};

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('department-head limit API client', () => {
  it('reads the summary for one nature', async () => {
    const calls = stubFetch(() => json(200, summary));

    const result = await getDeptHeadLimit('OPEX');

    expect(calls[0]?.url).toBe('/api/v1/dept-head-limits?nature=OPEX');
    expect(result.active?.amountPkr).toBe(50000);
  });

  it('refuses a summary that does not have the expected shape', async () => {
    stubFetch(() => json(200, { nature: 'OPEX' }));

    await expect(getDeptHeadLimit('OPEX')).rejects.toThrow('The response could not be read');
  });

  it('sends the amount, date and version when setting a limit', async () => {
    const calls = stubFetch(() => json(201, record));

    const saved = await saveDeptHeadLimit({
      nature: 'OPEX',
      amountPkr: 50000,
      description: null,
      effectiveDate: '2026-11-05',
      rowVersion: 1,
    });

    expect(saved.id).toBe(record.id);
    expect(calls[0]?.method).toBe('POST');
    expect(calls[0]?.body).toEqual({
      nature: 'OPEX',
      amountPkr: 50000,
      description: null,
      effectiveDate: '2026-11-05',
      rowVersion: 1,
    });
  });

  it('keeps the concurrency code for a save over a changed limit', async () => {
    stubFetch(() => json(412, { code: 'EDM-CONC-001', message: 'This record was changed by someone else. Please reload.' }));

    const error = await saveDeptHeadLimit({
      nature: 'OPEX',
      amountPkr: 50000,
      description: null,
      effectiveDate: '2026-11-05',
      rowVersion: 1,
    }).catch((e: unknown) => e);

    expect(error).toBeInstanceOf(ApiError);
    expect((error as ApiError).code).toBe('EDM-CONC-001');
  });

  it('sends the nature and version when ending the limit, and accepts no content', async () => {
    const calls = stubFetch(() => new Response(null, { status: 204 }));

    await deactivateDeptHeadLimit('CAPEX', 4);

    expect(calls[0]?.url).toBe('/api/v1/dept-head-limits/deactivate');
    expect(calls[0]?.body).toEqual({ nature: 'CAPEX', rowVersion: 4 });
  });
});

describe('department-head limit validation', () => {
  const base = { amount: '50000', description: '', effectiveDate: '2026-11-05', today: '2026-11-05' };

  it('accepts a whole or two-decimal amount, a date today or later, and an empty description', () => {
    expect(validateDeptHeadLimit(base)).toEqual({});
    expect(validateDeptHeadLimit({ ...base, amount: '50000.5' })).toEqual({});
    expect(validateDeptHeadLimit({ ...base, amount: '50000.50', effectiveDate: '2026-11-06' })).toEqual({});
  });

  it('asks for an amount', () => {
    expect(validateDeptHeadLimit({ ...base, amount: '  ' }).amountPkr).toBe('Enter an amount.');
  });

  it('refuses zero, negative and non-numeric amounts with a clear message', () => {
    expect(validateDeptHeadLimit({ ...base, amount: '0' }).amountPkr).toBe('The limit must be greater than zero.');
    expect(validateDeptHeadLimit({ ...base, amount: '0.00' }).amountPkr).toBe('The limit must be greater than zero.');
    expect(validateDeptHeadLimit({ ...base, amount: '-5' }).amountPkr).toBe(
      'Enter an amount in PKR, for example 50000 or 50000.50.',
    );
    expect(validateDeptHeadLimit({ ...base, amount: 'fifty' }).amountPkr).toBe(
      'Enter an amount in PKR, for example 50000 or 50000.50.',
    );
  });

  it('refuses more than two decimals', () => {
    expect(validateDeptHeadLimit({ ...base, amount: '50000.125' }).amountPkr).toBe('Use at most two decimal places.');
  });

  it('asks for a date and refuses a past date', () => {
    expect(validateDeptHeadLimit({ ...base, effectiveDate: '' }).effectiveDate).toBe('Choose an effective date.');
    expect(validateDeptHeadLimit({ ...base, effectiveDate: '2026-11-04' }).effectiveDate).toBe(
      'The effective date cannot be in the past.',
    );
  });

  it('refuses a description over 500 characters', () => {
    expect(validateDeptHeadLimit({ ...base, description: 'd'.repeat(501) }).description).toBe(
      'The description must be 500 characters or fewer.',
    );
  });

  it('reads today in Karachi, not in UTC', () => {
    // 20:00 UTC on 4 Nov is 01:00 on 5 Nov in Karachi.
    expect(karachiToday(new Date('2026-11-04T20:00:00Z'))).toBe('2026-11-05');
  });

  it('parses and formats amounts for display', () => {
    expect(parseAmount(' 50000.50 ')).toBe(50000.5);
    expect(formatPkr(50000)).toBe('PKR 50,000.00');
    expect(formatPkr(20000.5)).toBe('PKR 20,000.50');
  });
});
