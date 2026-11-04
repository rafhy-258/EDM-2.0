import { FluentProvider, webLightTheme } from '@fluentui/react-components';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { AuditActivityPage } from './AuditActivityPage';
import type { AuditEvent, AuditEventPage } from './auditApi';

function event(overrides: Partial<AuditEvent> = {}): AuditEvent {
  return {
    id: 'e-1',
    occurredAtUtc: '2026-11-04T04:30:00Z',
    actorUserId: null,
    actorName: 'Sara Ali',
    actorDepartment: 'Finance',
    actorRoles: ['AUDITOR'],
    correlationId: 'corr-1',
    module: 'Configuration',
    entityType: 'Category',
    entityId: 'CAT-001',
    action: 'Update',
    outcome: 'Success',
    failureReason: null,
    remarks: null,
    requestReference: null,
    policyVersion: null,
    before: null,
    after: null,
    changes: [{ field: 'Status', before: 'Draft', after: 'Active' }],
    ...overrides,
  };
}

function pageOf(items: AuditEvent[], totalCount = items.length): AuditEventPage {
  return { items, page: 1, pageSize: 25, totalCount };
}

function jsonResponse(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } });
}

function renderPage() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <FluentProvider theme={webLightTheme}>
        <AuditActivityPage />
      </FluentProvider>
    </QueryClientProvider>,
  );
}

function requestedUrls(fetchMock: ReturnType<typeof vi.fn>): URL[] {
  return fetchMock.mock.calls.map((call) => new URL(String(call[0]), 'http://localhost'));
}

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('Activity Logs viewer', () => {
  it('shows the events with the date and time in Asia/Karachi', async () => {
    const fetchMock = vi.fn(async () => jsonResponse(pageOf([event()])));
    vi.stubGlobal('fetch', fetchMock);

    renderPage();

    const table = await screen.findByRole('table', { name: 'Audit events' });
    const row = within(table).getByText('Sara Ali').closest('tr');
    if (!row) throw new Error('The Sara Ali row was not rendered');
    expect(within(row).getByText('04-Nov-2026')).toBeInTheDocument();
    expect(within(row).getByText('09:30:00')).toBeInTheDocument();
    expect(within(row).getByText('Category: CAT-001')).toBeInTheDocument();
    expect(within(row).getByText('Success')).toBeInTheDocument();

    const first = requestedUrls(fetchMock)[0];
    expect(first?.pathname).toBe('/api/v1/audit-events');
    expect(first?.searchParams.get('page')).toBe('1');
    expect(first?.searchParams.get('pageSize')).toBe('25');
  });

  it('sends the Karachi day as UTC bounds when a date range is applied', async () => {
    const fetchMock = vi.fn(async () => jsonResponse(pageOf([event()])));
    vi.stubGlobal('fetch', fetchMock);
    renderPage();
    await screen.findByRole('table', { name: 'Audit events' });

    fireEvent.change(screen.getByLabelText('From date'), { target: { value: '2026-11-04' } });
    fireEvent.change(screen.getByLabelText('To date'), { target: { value: '2026-11-04' } });
    fireEvent.click(screen.getByRole('button', { name: 'Apply filters' }));

    await waitFor(() => expect(fetchMock).toHaveBeenCalledTimes(2));
    const last = requestedUrls(fetchMock).at(-1);
    expect(last?.searchParams.get('from')).toBe('2026-11-03T19:00:00.000Z');
    expect(last?.searchParams.get('to')).toBe('2026-11-04T18:59:59.999Z');
    expect(last?.searchParams.get('page')).toBe('1');
  });

  it('blocks a date range that ends before it starts, without sending a request', async () => {
    const fetchMock = vi.fn(async () => jsonResponse(pageOf([event()])));
    vi.stubGlobal('fetch', fetchMock);
    renderPage();
    await screen.findByRole('table', { name: 'Audit events' });

    fireEvent.change(screen.getByLabelText('From date'), { target: { value: '2026-11-05' } });
    fireEvent.change(screen.getByLabelText('To date'), { target: { value: '2026-11-04' } });
    fireEvent.click(screen.getByRole('button', { name: 'Apply filters' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('The end date must not be earlier than the start date.');
    expect(fetchMock).toHaveBeenCalledTimes(1);
  });

  it('shows the empty state when no event matches', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => jsonResponse(pageOf([]))));

    renderPage();

    expect(await screen.findByRole('heading', { name: 'No audit events match these filters' })).toBeInTheDocument();
  });

  it('shows the sign-in state on 401', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => jsonResponse({ code: 'EDM-AUTH-001', message: 'Sign in required.' }, 401)),
    );

    renderPage();

    expect(await screen.findByRole('heading', { name: 'Please sign in' })).toBeInTheDocument();
  });

  it('shows the no-permission state on 403', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => jsonResponse({ code: 'EDM-AUTH-002', message: 'Not permitted.' }, 403)),
    );

    renderPage();

    expect(await screen.findByRole('heading', { name: 'You do not have access to this' })).toBeInTheDocument();
  });

  it('shows an error with a retry that loads the events again', async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce(jsonResponse({ code: 'EDM-SYS-001', message: 'Server error.' }, 500))
      .mockResolvedValueOnce(jsonResponse(pageOf([event()])));
    vi.stubGlobal('fetch', fetchMock);

    renderPage();

    expect(await screen.findByRole('heading', { name: 'Something went wrong' })).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Try again' }));

    expect(await screen.findByRole('table', { name: 'Audit events' })).toBeInTheDocument();
    expect(fetchMock).toHaveBeenCalledTimes(2);
  });

  it('pages on the server and shows the page count', async () => {
    const fetchMock = vi.fn(async () => jsonResponse(pageOf([event()], 60)));
    vi.stubGlobal('fetch', fetchMock);
    renderPage();

    expect(await screen.findByText('Page 1 of 3')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Next' }));

    await waitFor(() => expect(fetchMock).toHaveBeenCalledTimes(2));
    expect(requestedUrls(fetchMock).at(-1)?.searchParams.get('page')).toBe('2');
  });

  it('shows the field changes of an event in the details dialog', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async () =>
        jsonResponse(
          pageOf([
            event({
              outcome: 'Failure',
              failureReason: 'Limit above the maximum.',
              requestReference: 'REQ-2026-000042',
              policyVersion: 3,
            }),
          ]),
        ),
      ),
    );
    renderPage();
    await screen.findByRole('table', { name: 'Audit events' });

    fireEvent.click(screen.getByRole('button', { name: /View details of Update on Category CAT-001 at 09:30:00/ }));

    const dialog = await screen.findByRole('dialog');
    expect(within(dialog).getByText('Audit event')).toBeInTheDocument();
    expect(within(dialog).getByText('Failure: Limit above the maximum.')).toBeInTheDocument();
    expect(within(dialog).getByText('REQ-2026-000042')).toBeInTheDocument();
    expect(within(dialog).getByText('Policy version')).toBeInTheDocument();
    const changes = within(dialog).getByRole('table', { name: 'Field changes' });
    expect(within(changes).getByText('Status')).toBeInTheDocument();
    expect(within(changes).getByText('Draft')).toBeInTheDocument();
    expect(within(changes).getByText('Active')).toBeInTheDocument();
  });

  it('says so when an event has no field changes', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => jsonResponse(pageOf([event({ action: 'View', changes: null })]))));
    renderPage();
    await screen.findByRole('table', { name: 'Audit events' });

    fireEvent.click(screen.getByRole('button', { name: /View details of View/ }));

    expect(await screen.findByText('No field changes were recorded for this event.')).toBeInTheDocument();
  });
});
