import { FluentProvider, webLightTheme } from '@fluentui/react-components';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import type { Permission } from '../session/permissions';
import { devSessionFor, SessionProvider, type SessionUser } from '../session/session';
import { CategoriesPage } from './CategoriesPage';

interface Call {
  readonly url: string;
  readonly method: string;
  readonly body: unknown;
}

const travel = {
  id: '6f1e2a10-0000-4000-8000-000000000001',
  name: 'Travel',
  description: 'Flights and hotels',
  isActive: true,
  rowVersion: 1,
  subcategoryCount: 2,
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
          <CategoriesPage />
        </SessionProvider>
      </FluentProvider>
    </QueryClientProvider>,
  );
}

const administrator = devSessionFor('EDM_ADMINISTRATOR');
const viewer: SessionUser = {
  displayName: 'Read only',
  department: 'Finance',
  permissions: new Set<Permission>(['config.category.view']),
};

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('Categories screen', () => {
  it('lists categories with status, subcategory count and the Karachi date', async () => {
    stubApi(() => ({ status: 200, body: [travel] }));

    renderPage(administrator);

    expect(await screen.findByText('Flights and hotels')).toBeInTheDocument();
    expect(screen.getByText('Active')).toBeInTheDocument();
    expect(screen.getByText('2')).toBeInTheDocument();
    // 05:00 UTC is 10:00 in Asia/Karachi, the same day.
    expect(screen.getByText('04-Nov-2026')).toBeInTheDocument();
  });

  it('refuses an empty name on the client and sends nothing', async () => {
    const calls = stubApi(() => ({ status: 200, body: [] }));
    renderPage(administrator);

    fireEvent.click(await screen.findByRole('button', { name: 'New category' }));
    const dialog = screen.getByRole('dialog');
    fireEvent.click(within(dialog).getByRole('button', { name: 'Save' }));

    expect(await within(dialog).findByText('Enter a name.')).toBeInTheDocument();
    expect(calls.filter((c) => c.method !== 'GET')).toEqual([]);
  });

  it('creates a category with a trimmed name and closes the dialog', async () => {
    let created = false;
    const calls = stubApi((call) => {
      if (call.method === 'POST') {
        created = true;
        return { status: 201, body: { ...travel, id: 'new-id', name: 'Training', description: null, subcategoryCount: 0 } };
      }
      return { status: 200, body: created ? [{ ...travel, id: 'new-id', name: 'Training', description: null, subcategoryCount: 0 }] : [] };
    });
    renderPage(administrator);

    fireEvent.click(await screen.findByRole('button', { name: 'New category' }));
    const dialog = screen.getByRole('dialog');
    fireEvent.change(within(dialog).getByLabelText(/Name/), { target: { value: '  Training  ' } });
    fireEvent.click(within(dialog).getByRole('button', { name: 'Save' }));

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    const post = calls.find((c) => c.method === 'POST');
    expect(post?.url).toBe('/api/v1/categories');
    expect(post?.body).toEqual({ name: 'Training', description: null });
  });

  it('shows a duplicate name from the server on the name field', async () => {
    stubApi((call) =>
      call.method === 'POST'
        ? {
            status: 422,
            body: {
              code: 'EDM-VAL-001',
              message: 'Some fields need attention. Please check the highlighted fields.',
              errors: { name: ['A category with this name already exists.'] },
            },
          }
        : { status: 200, body: [travel] },
    );
    renderPage(administrator);

    fireEvent.click(await screen.findByRole('button', { name: 'New category' }));
    const dialog = screen.getByRole('dialog');
    fireEvent.change(within(dialog).getByLabelText(/Name/), { target: { value: 'Travel' } });
    fireEvent.click(within(dialog).getByRole('button', { name: 'Save' }));

    expect(await within(dialog).findByText('A category with this name already exists.')).toBeInTheDocument();
    expect(screen.getByRole('dialog')).toBeInTheDocument();
  });

  it('shows the server message when a delete is refused because the category is in use', async () => {
    stubApi((call) =>
      call.method === 'DELETE'
        ? {
            status: 409,
            body: { code: 'EDM-CFG-003', message: 'This record is used by other records and cannot be deleted.' },
          }
        : { status: 200, body: [travel] },
    );
    renderPage(administrator);

    fireEvent.click(await screen.findByRole('button', { name: 'Delete' }));
    const confirm = screen.getByRole('dialog');
    fireEvent.click(within(confirm).getByRole('button', { name: 'Delete' }));

    expect(
      await screen.findByText('This record is used by other records and cannot be deleted.'),
    ).toBeInTheDocument();
  });

  it('hides the change actions from a user who can only view categories', async () => {
    stubApi(() => ({ status: 200, body: [travel] }));

    renderPage(viewer);

    expect(await screen.findByText('Flights and hotels')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'New category' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Edit' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Delete' })).not.toBeInTheDocument();
  });
});
