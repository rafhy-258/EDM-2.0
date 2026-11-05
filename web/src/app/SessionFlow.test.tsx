import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { App } from './App';

const AUDITOR_SESSION = {
  userId: '5b0a1c2d-0000-4000-8000-000000000042',
  username: 'auditor.one',
  displayName: 'Auditor One',
  department: 'Compliance',
  roles: ['AUDITOR'],
  permissions: ['audit.view', 'audit.export', 'report.view.all', 'report.export'],
};

const EMPTY_AUDIT_PAGE = { items: [], page: 1, pageSize: 25, totalCount: 0 };

function json(status: number, body: unknown): Response {
  return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } });
}

function navLabels(): string[] {
  const nav = screen.getByRole('navigation', { name: 'Main navigation' });
  return within(nav)
    .queryAllByRole('link')
    .map((link) => link.textContent ?? '');
}

function stubServer(session: 'signed-out' | 'auditor') {
  const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const url = String(input);
    if (url.endsWith('/api/v1/auth/session')) {
      return session === 'auditor'
        ? json(200, AUDITOR_SESSION)
        : json(401, { code: 'EDM-AUTH-001', message: 'Please sign in.' });
    }
    if (url.endsWith('/api/v1/auth/sign-out') && init?.method === 'POST') {
      return new Response(null, { status: 204 });
    }
    return json(200, EMPTY_AUDIT_PAGE);
  });
  vi.stubGlobal('fetch', fetchMock);
  return fetchMock;
}

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('session from the server (no development fixture)', () => {
  it('shows the sign-in page, with no menu, when the server has no session', async () => {
    stubServer('signed-out');
    window.history.pushState({}, '', '/workspace/overview');

    render(<App />);

    expect(await screen.findByRole('heading', { name: 'Sign in to your account' })).toBeInTheDocument();
    expect(screen.queryByRole('navigation', { name: 'Main navigation' })).toBeNull();
    expect(screen.queryByRole('combobox', { name: 'Development role' })).toBeNull();
  });

  it('shows the menu for the user the server reports, and the sign-out control', async () => {
    stubServer('auditor');
    window.history.pushState({}, '', '/workspace/overview');

    render(<App />);

    await waitFor(() => expect(navLabels()).toContain('Activity Logs'));
    expect(navLabels()).not.toContain('Users');
    expect(screen.getByRole('button', { name: 'Sign out' })).toBeInTheDocument();
  });

  it('signs out and returns to the sign-in page', async () => {
    const fetchMock = stubServer('auditor');
    window.history.pushState({}, '', '/workspace/overview');
    render(<App />);
    await waitFor(() => expect(navLabels()).toContain('Activity Logs'));

    fireEvent.click(screen.getByRole('button', { name: 'Sign out' }));

    expect(await screen.findByRole('heading', { name: 'Sign in to your account' })).toBeInTheDocument();
    const signOutCall = fetchMock.mock.calls.find(([input]) => String(input).endsWith('/api/v1/auth/sign-out'));
    expect(signOutCall).toBeDefined();
  });
});
