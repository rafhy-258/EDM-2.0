import { FluentProvider, webLightTheme } from '@fluentui/react-components';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import type { Permission } from '../session/permissions';
import { devSessionFor, SessionProvider, type SessionUser } from '../session/session';
import { FinancialLimitsPage } from './FinancialLimitsPage';
import { karachiToday } from './configurationValidation';

interface Call {
  readonly url: string;
  readonly method: string;
  readonly body: unknown;
}

const defaultSummary = {
  nature: 'OPEX',
  currentAmountPkr: 20000,
  isDefault: true,
  status: 'Default',
  defaultAmountPkr: 20000,
  active: null,
  lastChangedAtUtc: null,
  history: [],
};

const activeRecord = {
  id: '6f1e2a10-0000-4000-8000-000000000010',
  nature: 'OPEX',
  amountPkr: 50000,
  description: 'Department heads',
  effectiveFrom: '2026-11-04T19:00:00Z',
  effectiveTo: null,
  isActive: true,
  rowVersion: 3,
  updatedAtUtc: '2026-11-04T05:00:00Z',
  updatedBy: null,
};

const activeSummary = {
  ...defaultSummary,
  currentAmountPkr: 50000,
  isDefault: false,
  status: 'Active',
  active: activeRecord,
  lastChangedAtUtc: activeRecord.updatedAtUtc,
  history: [activeRecord],
};

/** Routes each request to a response, and records the calls. */
function stubApi(respond: (call: Call) => { status: number; body?: unknown }): Call[] {
  const calls: Call[] = [];
  vi.stubGlobal(
    'fetch',
    vi.fn((url: string, init?: RequestInit) => {
      const call: Call = { url, method: init?.method ?? 'GET', body: init?.body ? JSON.parse(String(init.body)) : undefined };
      calls.push(call);
      const result = respond(call);
      const body = result.body === undefined ? null : JSON.stringify(result.body);
      return Promise.resolve(new Response(body, { status: result.status, headers: { 'Content-Type': 'application/json' } }));
    }),
  );
  return calls;
}

function renderPage(user: SessionUser) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <FluentProvider theme={webLightTheme}>
        <SessionProvider user={user}>
          <FinancialLimitsPage />
        </SessionProvider>
      </FluentProvider>
    </QueryClientProvider>,
  );
}

/** The change buttons are disabled until the summary loads. A click on a disabled button does nothing, so wait first. */
async function enabledButton(name: string): Promise<HTMLElement> {
  const button = await screen.findByRole('button', { name });
  await waitFor(() => expect(button).toBeEnabled());
  return button;
}

const administrator = devSessionFor('EDM_ADMINISTRATOR');
const viewer: SessionUser = {
  displayName: 'Read only',
  department: 'Finance',
  permissions: new Set<Permission>(['audit.view']),
};

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('Financial limits screen', () => {
  it('shows the seeded default when no limit is set, for OPEX', async () => {
    const calls = stubApi(() => ({ status: 200, body: defaultSummary }));

    renderPage(administrator);

    expect(await screen.findByText('PKR 20,000.00')).toBeInTheDocument();
    expect(screen.getByText('System default. No limit is set.')).toBeInTheDocument();
    expect(screen.getByText('No active limit. The default applies.')).toBeInTheDocument();
    expect(calls[0]?.url).toBe('/api/v1/dept-head-limits?nature=OPEX');
  });

  it('shows the active limit, its status and its history', async () => {
    stubApi(() => ({ status: 200, body: activeSummary }));

    renderPage(administrator);

    expect((await screen.findAllByText('PKR 50,000.00')).length).toBeGreaterThan(0);
    expect(screen.getByText('Applies now.')).toBeInTheDocument();
    expect(screen.getByText('Department heads')).toBeInTheDocument();
    expect(screen.getAllByText('Active').length).toBeGreaterThan(0);
  });

  it('switching to CAPEX asks the server for the CAPEX limit', async () => {
    const calls = stubApi((call) => ({
      status: 200,
      body: call.url.endsWith('CAPEX') ? { ...defaultSummary, nature: 'CAPEX' } : defaultSummary,
    }));
    renderPage(administrator);
    await screen.findByText('PKR 20,000.00');

    fireEvent.click(screen.getByRole('tab', { name: 'CAPEX' }));

    await waitFor(() => expect(calls.some((c) => c.url === '/api/v1/dept-head-limits?nature=CAPEX')).toBe(true));
  });

  it('creates a currency from the Financial Limits currency tab without converting its rate to a number', async () => {
    const savedCurrency = {
      id: '00000000-0000-7000-8000-000000000101',
      code: 'USD',
      name: 'US Dollar',
      isBase: false,
      isActive: true,
      rate: '278.1234',
      rowVersion: 1,
      updatedAtUtc: '2026-11-05T05:00:00Z',
      updatedBy: null,
    };
    const calls = stubApi((call) => {
      if (call.method === 'POST') {
        return { status: 201, body: savedCurrency };
      }
      if (call.url.startsWith('/api/v1/currencies')) {
        return { status: 200, body: [] };
      }
      return { status: 200, body: defaultSummary };
    });
    renderPage(administrator);

    await screen.findByText('PKR 20,000.00');
    fireEvent.click(screen.getByRole('tab', { name: 'Currencies' }));
    fireEvent.click(await enabledButton('New currency'));
    const dialog = screen.getByRole('dialog');
    fireEvent.change(within(dialog).getByLabelText(/Currency code/), { target: { value: 'usd' } });
    fireEvent.change(within(dialog).getByLabelText(/Currency name/), { target: { value: 'US Dollar' } });
    fireEvent.change(within(dialog).getByLabelText(/Rate \(PKR per currency unit\)/), { target: { value: '278.1234' } });
    fireEvent.click(within(dialog).getByRole('button', { name: 'Save' }));

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    const post = calls.find((call) => call.method === 'POST');
    expect(post?.url).toBe('/api/v1/currencies');
    expect(post?.body).toEqual({ code: 'USD', name: 'US Dollar', rate: '278.1234' });
  });

  it('sets a new limit with the amount, the Karachi date and no version when none is active', async () => {
    let saved = false;
    const calls = stubApi((call) => {
      if (call.method === 'POST') {
        saved = true;
        return { status: 201, body: activeRecord };
      }
      return { status: 200, body: saved ? activeSummary : defaultSummary };
    });
    renderPage(administrator);

    fireEvent.click(await enabledButton('Set new limit'));
    const dialog = screen.getByRole('dialog');
    fireEvent.change(within(dialog).getByLabelText(/Limit \(PKR\)/), { target: { value: '50000.50' } });
    fireEvent.click(within(dialog).getByRole('button', { name: 'Save limit' }));

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    const post = calls.find((c) => c.method === 'POST');
    expect(post?.url).toBe('/api/v1/dept-head-limits');
    expect(post?.body).toEqual({
      nature: 'OPEX',
      amountPkr: 50000.5,
      description: null,
      effectiveDate: karachiToday(),
      rowVersion: null,
    });
  });

  it('refuses a zero limit on the client and sends nothing', async () => {
    const calls = stubApi(() => ({ status: 200, body: defaultSummary }));
    renderPage(administrator);

    fireEvent.click(await enabledButton('Set new limit'));
    const dialog = screen.getByRole('dialog');
    fireEvent.change(within(dialog).getByLabelText(/Limit \(PKR\)/), { target: { value: '0' } });
    fireEvent.click(within(dialog).getByRole('button', { name: 'Save limit' }));

    expect(await within(dialog).findByText('The limit must be greater than zero.')).toBeInTheDocument();
    expect(calls.filter((c) => c.method !== 'GET')).toEqual([]);
  });

  it('refuses an amount with three decimals on the client', async () => {
    stubApi(() => ({ status: 200, body: defaultSummary }));
    renderPage(administrator);

    fireEvent.click(await enabledButton('Set new limit'));
    const dialog = screen.getByRole('dialog');
    fireEvent.change(within(dialog).getByLabelText(/Limit \(PKR\)/), { target: { value: '50000.125' } });
    fireEvent.click(within(dialog).getByRole('button', { name: 'Save limit' }));

    expect(await within(dialog).findByText('Use at most two decimal places.')).toBeInTheDocument();
  });

  it('shows a server field error on the field', async () => {
    stubApi((call) =>
      call.method === 'POST'
        ? {
            status: 422,
            body: {
              code: 'EDM-VAL-001',
              message: 'Some fields need attention. Please check the highlighted fields.',
              errors: { effectiveDate: ['The effective date cannot be in the past.'] },
            },
          }
        : { status: 200, body: defaultSummary },
    );
    renderPage(administrator);

    fireEvent.click(await enabledButton('Set new limit'));
    const dialog = screen.getByRole('dialog');
    fireEvent.change(within(dialog).getByLabelText(/Limit \(PKR\)/), { target: { value: '50000' } });
    fireEvent.click(within(dialog).getByRole('button', { name: 'Save limit' }));

    expect(await within(dialog).findByText('The effective date cannot be in the past.')).toBeInTheDocument();
  });

  it('shows the stale-change message when the limit was changed by someone else', async () => {
    stubApi((call) =>
      call.method === 'POST'
        ? {
            status: 412,
            body: { code: 'EDM-CONC-001', message: 'This record was changed by someone else. Please reload.' },
          }
        : { status: 200, body: defaultSummary },
    );
    renderPage(administrator);

    fireEvent.click(await enabledButton('Set new limit'));
    const dialog = screen.getByRole('dialog');
    fireEvent.change(within(dialog).getByLabelText(/Limit \(PKR\)/), { target: { value: '50000' } });
    fireEvent.click(within(dialog).getByRole('button', { name: 'Save limit' }));

    expect(await within(dialog).findByText('This record was changed by someone else. Please reload.')).toBeInTheDocument();
  });

  it('ends the active limit after confirmation, with its version', async () => {
    const calls = stubApi((call) => {
      if (call.url.endsWith('/deactivate')) {
        return { status: 204 };
      }
      return { status: 200, body: activeSummary };
    });
    renderPage(administrator);

    fireEvent.click(await enabledButton('End current limit'));
    const confirm = screen.getByRole('dialog');
    fireEvent.click(within(confirm).getByRole('button', { name: 'End limit' }));

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    const post = calls.find((c) => c.url.endsWith('/deactivate'));
    expect(post?.method).toBe('POST');
    expect(post?.body).toEqual({ nature: 'OPEX', rowVersion: 3 });
  });

  it('hides the change buttons from a user without config.limit.manage', async () => {
    stubApi(() => ({ status: 200, body: activeSummary }));

    renderPage(viewer);

    expect((await screen.findAllByText('PKR 50,000.00')).length).toBeGreaterThan(0);
    expect(screen.queryByRole('button', { name: 'Set new limit' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'End current limit' })).not.toBeInTheDocument();
  });
});
