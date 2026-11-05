import { FluentProvider, webLightTheme } from '@fluentui/react-components';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import type { Permission } from '../session/permissions';
import { devSessionFor, SessionProvider, type SessionUser } from '../session/session';
import { UsersPage } from './UsersPage';

const finance = { id: '6f1e2a10-0000-4000-8000-0000000000f1', name: 'Finance' };

const ali = {
  id: '6f1e2a10-0000-4000-8000-0000000000a1',
  employeeId: 'EMP1024',
  name: 'Ali Khan',
  email: 'ali@example.com',
  departmentId: finance.id,
  isActive: true,
  rowVersion: 1,
  updatedAtUtc: '2026-11-04T05:00:00Z',
  updatedBy: null,
};

/** Answers the department options and the people list, by URL. */
function stubApi(): void {
  vi.stubGlobal(
    'fetch',
    vi.fn((url: string) => {
      const body = url.startsWith('/api/v1/departments/options') ? [finance] : [ali];
      return Promise.resolve(
        new Response(JSON.stringify(body), { status: 200, headers: { 'Content-Type': 'application/json' } }),
      );
    }),
  );
}

function renderPage(user: SessionUser) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <FluentProvider theme={webLightTheme}>
        <SessionProvider user={user}>
          <UsersPage />
        </SessionProvider>
      </FluentProvider>
    </QueryClientProvider>,
  );
}

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('Users screen', () => {
  it('lists people with their department name and status', async () => {
    stubApi();

    renderPage(devSessionFor('SYSTEM_ADMINISTRATOR'));

    expect(await screen.findByText('EMP1024')).toBeInTheDocument();
    expect(screen.getByText('Ali Khan')).toBeInTheDocument();
    expect(await screen.findByText('Finance')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Add person' })).toBeEnabled();
  });

  it('hides the add and change actions from a user who can only view people', async () => {
    stubApi();
    const viewer: SessionUser = {
      displayName: 'Directory viewer',
      department: 'HR',
      permissions: new Set<Permission>(['org.user.view']),
    };

    renderPage(viewer);

    expect(await screen.findByText('Ali Khan')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Add person' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Edit' })).not.toBeInTheDocument();
  });
});
