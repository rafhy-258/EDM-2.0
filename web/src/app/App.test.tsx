import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import axe from 'axe-core';
import { describe, expect, it } from 'vitest';
import { App } from './App';

function openAt(path: string) {
  window.history.pushState({}, '', path);
  return render(<App devFixtures />);
}

function selectRole(value: string) {
  const select = screen.getByRole('combobox', { name: 'Development role' });
  fireEvent.change(select, { target: { value } });
}

function navLabels(): string[] {
  const nav = screen.getByRole('navigation', { name: 'Main navigation' });
  return within(nav)
    .queryAllByRole('link')
    .map((link) => link.textContent ?? '');
}

describe('application shell', () => {
  it('shows the menu for a Requester: their items are present and administration items are absent', () => {
    openAt('/workspace/overview');
    const labels = navLabels();
    expect(labels).toContain('Create Expense');
    expect(labels).toContain('My Requests');
    expect(labels).toContain('Overview');
    expect(labels).not.toContain('Approval Policies');
    expect(labels).not.toContain('Users');
    expect(labels).not.toContain('Activity Logs');
  });

  it('never shows a Phase 2 item, even in development', () => {
    openAt('/workspace/overview');
    const labels = navLabels();
    for (const phase2 of ['Drafts', 'Workflow Designer', 'Delegation', 'Escalations', 'Rule Builder', 'ERP', 'Webhooks']) {
      expect(labels, phase2).not.toContain(phase2);
    }
  });

  it('redirects the root path to the Overview', async () => {
    openAt('/');
    await waitFor(() => expect(screen.getByRole('heading', { name: 'Overview', level: 1 })).toBeInTheDocument());
  });

  it('blocks a screen the role has no permission for, with the EDM-AUTH-002 message', () => {
    openAt('/configuration/categories');
    expect(screen.getByRole('heading', { name: 'You do not have access to this' })).toBeInTheDocument();
  });

  it('shows the screen after the role changes to one that holds the permission, and hides the menu item again on change back', () => {
    openAt('/configuration/categories');
    selectRole('EDM_ADMINISTRATOR');
    expect(screen.getByRole('heading', { name: 'Categories', level: 1 })).toBeInTheDocument();
    expect(navLabels()).toContain('Approval Policies');

    selectRole('REQUESTER');
    expect(navLabels()).not.toContain('Categories');
  });

  it('shows the sign-in state, with no menu items, when there is no session (deny by default)', () => {
    openAt('/workspace/overview');
    selectRole('NONE');
    expect(screen.getByRole('heading', { name: 'Sign in to your account' })).toBeInTheDocument();
    expect(screen.queryByRole('navigation', { name: 'Main navigation' })).toBeNull();
  });

  it('shows the not-found page for an unknown address', () => {
    openAt('/does-not-exist');
    expect(screen.getByRole('heading', { name: 'Page not found' })).toBeInTheDocument();
  });

  it('has no automatically detectable accessibility violations on the Overview', async () => {
    const { container } = openAt('/workspace/overview');
    const result = await axe.run(container, {
      // jsdom cannot compute rendered colours, so contrast is checked in the Playwright run.
      rules: { 'color-contrast': { enabled: false } },
    });
    expect(result.violations.map((v) => `${v.id}: ${v.help}`)).toEqual([]);
  });
});
