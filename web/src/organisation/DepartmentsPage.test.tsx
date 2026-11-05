import { FluentProvider, webLightTheme } from '@fluentui/react-components';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import type { Permission } from '../session/permissions';
import { devSessionFor, SessionProvider, type SessionUser } from '../session/session';
import { DepartmentsPage } from './DepartmentsPage';

interface Call {
  readonly url: string;
  readonly method: string;
  readonly body: unknown;
}

const finance = {
  id: '6f1e2a10-0000-4000-8000-000000000001',
  name: 'Finance',
  isActive: true,
  rowVersion: 1,
  updatedAtUtc: '2026-11-04T05:00:00Z',
  updatedBy: null,
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
          <DepartmentsPage />
        </SessionProvider>
      </FluentProvider>
    </QueryClientProvider>,
  );
}

const administrator = devSessionFor('SYSTEM_ADMINISTRATOR');
const viewer: SessionUser = {
  displayName: 'Read only',
  department: 'Finance',
  permissions: new Set<Permission>(['org.user.view']),
};

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('Departments screen', () => {
  it('lists departments with their status and the Karachi date', async () => {
    stubApi(() => ({ status: 200, body: [finance] }));

    renderPage(administrator);

    expect(await screen.findByText('Finance')).toBeInTheDocument();
    expect(screen.getByText('Active')).toBeInTheDocument();
    // 05:00 UTC is 10:00 in Asia/Karachi, the same day.
    expect(screen.getByText('04-Nov-2026')).toBeInTheDocument();
  });

  it('refuses an empty name on the client and sends nothing', async () => {
    const calls = stubApi(() => ({ status: 200, body: [] }));
    renderPage(administrator);

    fireEvent.click(await screen.findByRole('button', { name: 'New department' }));
    const dialog = screen.getByRole('dialog');
    fireEvent.click(within(dialog).getByRole('button', { name: 'Save' }));

    expect(await within(dialog).findByText('Enter a department name.')).toBeInTheDocument();
    expect(calls.filter((c) => c.method !== 'GET')).toEqual([]);
  });

  it('creates a department with a trimmed name and closes the dialog', async () => {
    let created = false;
    const calls = stubApi((call) => {
      if (call.method === 'POST') {
        created = true;
        return { status: 201, body: { ...finance, id: 'new-id', name: 'HR' } };
      }
      return { status: 200, body: created ? [{ ...finance, id: 'new-id', name: 'HR' }] : [] };
    });
    renderPage(administrator);

    fireEvent.click(await screen.findByRole('button', { name: 'New department' }));
    const dialog = screen.getByRole('dialog');
    fireEvent.change(within(dialog).getByLabelText(/Name/), { target: { value: '  HR  ' } });
    fireEvent.click(within(dialog).getByRole('button', { name: 'Save' }));

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    const post = calls.find((c) => c.method === 'POST');
    expect(post?.url).toBe('/api/v1/departments');
    expect(post?.body).toEqual({ name: 'HR' });
  });

  it('shows a duplicate name from the server on the name field', async () => {
    stubApi((call) =>
      call.method === 'POST'
        ? {
            status: 422,
            body: {
              code: 'EDM-VAL-001',
              message: 'Some fields need attention. Please check the highlighted fields.',
              errors: { name: ['A department with this name already exists.'] },
            },
          }
        : { status: 200, body: [finance] },
    );
    renderPage(administrator);

    fireEvent.click(await screen.findByRole('button', { name: 'New department' }));
    const dialog = screen.getByRole('dialog');
    fireEvent.change(within(dialog).getByLabelText(/Name/), { target: { value: 'Finance' } });
    fireEvent.click(within(dialog).getByRole('button', { name: 'Save' }));

    expect(await within(dialog).findByText('A department with this name already exists.')).toBeInTheDocument();
  });

  it('asks for confirmation before deactivating and shows the server message when people are still active', async () => {
    const calls = stubApi((call) =>
      call.method === 'PUT'
        ? {
            status: 422,
            body: {
              code: 'EDM-VAL-001',
              message: 'Some fields need attention. Please check the highlighted fields.',
              errors: { isActive: ['Move or deactivate the people in this department first.'] },
            },
          }
        : { status: 200, body: [finance] },
    );
    renderPage(administrator);

    fireEvent.click(await screen.findByRole('button', { name: 'Deactivate' }));
    const confirm = screen.getByRole('dialog');
    fireEvent.click(within(confirm).getByRole('button', { name: 'Deactivate' }));

    expect(await screen.findByText('Some fields need attention. Please check the highlighted fields.')).toBeInTheDocument();
    const put = calls.find((c) => c.method === 'PUT');
    expect(put?.body).toEqual({ name: 'Finance', isActive: false, rowVersion: 1 });
  });

  it('hides the change actions from a user who cannot manage departments', async () => {
    stubApi(() => ({ status: 200, body: [finance] }));

    renderPage(viewer);

    expect(await screen.findByText('Finance')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'New department' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Rename' })).not.toBeInTheDocument();
  });
});
