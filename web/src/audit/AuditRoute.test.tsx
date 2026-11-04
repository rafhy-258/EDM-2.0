import { fireEvent, render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { App } from '../app/App';

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('Activity Logs route', () => {
  it('opens the audit viewer, not a placeholder, for a role that holds audit.view', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => new Response(JSON.stringify({ items: [], page: 1, pageSize: 25, totalCount: 0 }), { status: 200 })),
    );
    window.history.pushState({}, '', '/audit/activity');

    render(<App devFixtures />);
    fireEvent.change(screen.getByRole('combobox', { name: 'Development role' }), { target: { value: 'AUDITOR' } });

    expect(await screen.findByRole('heading', { name: 'Activity Logs', level: 1 })).toBeInTheDocument();
    expect(await screen.findByRole('heading', { name: 'No audit events match these filters' })).toBeInTheDocument();
  });
});
