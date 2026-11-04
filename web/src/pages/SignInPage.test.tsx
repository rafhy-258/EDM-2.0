import { FluentProvider, webLightTheme } from '@fluentui/react-components';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { fireEvent, render, screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { afterEach, describe, expect, it, vi } from 'vitest';
import type { RoleKey } from '../session/roles';
import { SignInPage } from './SignInPage';

const SESSION_BODY = {
  userId: '5b0a1c2d-0000-4000-8000-000000000042',
  username: 'sara.khan',
  displayName: 'Sara Khan',
  department: 'Finance',
  roles: ['AUDITOR'],
  permissions: ['audit.view'],
};

function jsonResponse(status: number, body: unknown): Response {
  return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } });
}

function renderPage(onDevRole?: (role: RoleKey) => void) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <QueryClientProvider client={client}>
      <FluentProvider theme={webLightTheme}>
        <MemoryRouter initialEntries={['/sign-in']}>
          <Routes>
            <Route path="/sign-in" element={<SignInPage onDevRole={onDevRole} />} />
            <Route path="/workspace/overview" element={<h1>Overview arrived</h1>} />
          </Routes>
        </MemoryRouter>
      </FluentProvider>
    </QueryClientProvider>,
  );
  return client;
}

function type(label: RegExp, value: string) {
  fireEvent.change(screen.getByLabelText(label), { target: { value } });
}

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('sign-in page', () => {
  it('posts the credentials as JSON and opens the Overview after a successful sign-in', async () => {
    const fetchMock = vi.fn(async () => jsonResponse(200, SESSION_BODY));
    vi.stubGlobal('fetch', fetchMock);
    renderPage();

    type(/username/i, 'Sara.Khan');
    type(/password/i, 'a long passphrase');
    fireEvent.click(screen.getByRole('button', { name: 'Sign in' }));

    expect(await screen.findByText('Overview arrived')).toBeInTheDocument();
    const [url, init] = fetchMock.mock.calls[0] as unknown as [string, RequestInit];
    expect(url).toBe('/api/v1/auth/sign-in');
    expect(init.method).toBe('POST');
    expect(JSON.parse(String(init.body))).toEqual({ username: 'Sara.Khan', password: 'a long passphrase' });
  });

  it('shows one message for a wrong password, so the page does not reveal which accounts exist', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => jsonResponse(401, { code: 'EDM-AUTH-001', message: 'Please sign in.' })));
    renderPage();

    type(/username/i, 'sara.khan');
    type(/password/i, 'wrong password');
    fireEvent.click(screen.getByRole('button', { name: 'Sign in' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('The username or password is not correct.');
  });

  it('asks for both fields before it calls the server', () => {
    const fetchMock = vi.fn();
    vi.stubGlobal('fetch', fetchMock);
    renderPage();

    fireEvent.click(screen.getByRole('button', { name: 'Sign in' }));

    expect(screen.getByRole('alert')).toHaveTextContent('Enter your username and password.');
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it('offers the development role fixture only when a handler is given', () => {
    renderPage();

    expect(screen.queryByRole('button', { name: 'Continue as this role' })).toBeNull();
  });

  it('passes the chosen role to the development handler', () => {
    const onDevRole = vi.fn();
    renderPage(onDevRole);

    fireEvent.click(screen.getByRole('button', { name: 'Continue as this role' }));

    expect(onDevRole).toHaveBeenCalledWith('REQUESTER');
  });
});
