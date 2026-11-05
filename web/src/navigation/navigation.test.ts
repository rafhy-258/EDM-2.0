import { describe, expect, it } from 'vitest';
import { PERMISSIONS } from '../session/permissions';
import { ROLE_KEYS, ROLE_PERMISSIONS } from '../session/roles';
import { NAV_AREAS, NAVIGATION } from './navigation';
import { groupByArea, menuFor, visibleNavigation } from './visibleNavigation';

/** Every Phase 1 and Phase 2 item named in Part 14.1, in order. */
const PART_14_1: Record<string, Array<[label: string, phase: 'P1' | 'P2']>> = {
  Workspace: [
    ['Overview', 'P1'],
    ['My Tasks', 'P1'],
    ['My Requests', 'P1'],
    ['Notifications', 'P1'],
  ],
  'Expense Management': [
    ['Create Expense', 'P1'],
    ['OPEX', 'P1'],
    ['CAPEX', 'P1'],
    ['Drafts', 'P2'],
    ['Submitted Requests', 'P1'],
    ['Expense Register', 'P1'],
  ],
  'Workflow and Approvals': [
    ['Approval Inbox', 'P1'],
    ['Workflow Designer', 'P2'],
    ['Approval Policies', 'P1'],
    ['Delegation', 'P2'],
    ['Escalations', 'P2'],
  ],
  'Organisation Management': [
    ['Departments', 'P1'],
    ['Branches', 'P1'],
    ['Business Units', 'P2'],
    ['Positions', 'P2'],
    ['Organisational Hierarchy', 'P1'],
  ],
  'Configuration and Policies': [
    ['Categories', 'P1'],
    ['Subcategories', 'P1'],
    ['Financial Limits', 'P1'],
    ['Approval Authorities', 'P1'],
    ['OPEX/CAPEX Policies', 'P1'],
    ['Rule Builder', 'P2'],
  ],
  'Identity and Access': [
    ['Users', 'P1'],
    ['Roles', 'P1'],
    ['Permissions', 'P1'],
    ['Access Policies', 'P2'],
    ['SSO', 'P1'],
    ['MFA', 'P1'],
  ],
  'Reports and Analytics': [
    ['Expense Reports', 'P1'],
    ['Approval Performance', 'P2'],
    ['Department Spending', 'P1'],
    ['Audit Reports', 'P1'],
    ['Exports', 'P1'],
  ],
  'Audit and Compliance': [
    ['Activity Logs', 'P1'],
    ['Approval History', 'P1'],
    ['Policy Changes', 'P1'],
    ['Security Events', 'P1'],
    ['Audit Exports', 'P1'],
  ],
  Integrations: [
    ['ERP', 'P2'],
    ['HRMS', 'P2'],
    ['Financial Systems', 'P2'],
    ['External APIs', 'P2'],
    ['Webhooks', 'P2'],
    ['Identity Providers', 'P1'],
  ],
  'System Administration': [
    ['Tenant Settings', 'P2'],
    ['General Settings', 'P1'],
    ['Notifications', 'P1'],
    ['Localisation', 'P1'],
    ['Data Retention', 'P1'],
    ['System Health', 'P1'],
  ],
};

describe('navigation data (Part 14.1)', () => {
  it('contains every item named in Part 14.1 with the correct phase', () => {
    for (const [area, expected] of Object.entries(PART_14_1)) {
      const actual = NAVIGATION.filter((item) => item.area === area).map((item) => [item.label, item.phase]);
      expect(actual, area).toEqual(expected);
    }
  });

  it('has exactly the ten areas of Part 14.1, in order', () => {
    expect(NAV_AREAS).toEqual(Object.keys(PART_14_1));
  });

  it('has unique ids and unique routes', () => {
    const ids = NAVIGATION.map((item) => item.id);
    const routes = NAVIGATION.map((item) => item.route);
    expect(new Set(ids).size).toBe(ids.length);
    expect(new Set(routes).size).toBe(routes.length);
  });

  it('uses only permissions from the Part 20 catalogue', () => {
    const catalogue = new Set<string>(PERMISSIONS);
    for (const item of NAVIGATION) {
      if (item.requiredPermission !== null) {
        expect(catalogue.has(item.requiredPermission), item.id).toBe(true);
      }
      for (const permission of item.requiredAnyPermission ?? []) {
        expect(catalogue.has(permission), `${item.id} alternative`).toBe(true);
      }
    }
  });

  it('makes every Phase 1 screen reachable by at least one role fixture (no orphan screens)', () => {
    const fixtureRoles = ROLE_KEYS.map((role) => new Set<string>(ROLE_PERMISSIONS[role]));
    for (const item of NAVIGATION.filter((i) => i.phase === 'P1')) {
      const required = item.requiredAnyPermission ?? (item.requiredPermission ? [item.requiredPermission] : []);
      if (required.length === 0) continue;
      const reachable = fixtureRoles.some((perms) => required.some((permission) => perms.has(permission)));
      expect(reachable, `${item.label} needs one of ${required.join(', ')}`).toBe(true);
    }
  });

  it('gives every route a leading slash and a lowercase path', () => {
    for (const item of NAVIGATION) {
      expect(item.route, item.id).toMatch(/^\/[a-z0-9/-]+$/);
    }
  });
});

describe('visibleNavigation', () => {
  it('never returns a Phase 2 item, even when every permission is granted', () => {
    const visible = visibleNavigation(NAVIGATION, () => true);
    expect(visible.length).toBeGreaterThan(0);
    expect(visible.every((item) => item.phase === 'P1')).toBe(true);
    expect(visible.map((item) => item.label)).not.toContain('Drafts');
    expect(visible.map((item) => item.label)).not.toContain('Workflow Designer');
  });

  it('shows only items whose permission the session holds, and always shows items with no permission', () => {
    const onlyAudit = (p: string | null) => p === null || p === 'audit.view';
    const visible = visibleNavigation(NAVIGATION, onlyAudit);
    const labels = visible.map((item) => item.label);
    expect(labels).toContain('Activity Logs');
    expect(labels).toContain('Overview');
    expect(labels).not.toContain('Users');
    expect(labels).not.toContain('Categories');
  });

  it('shows Approval Authorities when the session has any module-specific management permission', () => {
    for (const permission of ['config.ec.pool.manage', 'config.ra.manage', 'config.actiontags.manage']) {
      const visible = visibleNavigation(NAVIGATION, (candidate) => candidate === permission);
      expect(visible.map((item) => item.id)).toContain('approval-authorities');
    }
  });

  it('returns nothing except open items when there is no session', () => {
    const none = (p: string | null) => p === null && false;
    expect(visibleNavigation(NAVIGATION, none)).toEqual([]);
  });
});

describe('groupByArea and menuFor', () => {
  it('keeps the area order and drops empty areas', () => {
    const groups = groupByArea(NAVIGATION.filter((item) => item.phase === 'P1' && item.area === 'Workspace'));
    expect(groups.map((g) => g.area)).toEqual(['Workspace']);
  });

  it('menuFor groups the visible menu for a permission check', () => {
    const groups = menuFor(() => true);
    expect(groups.map((g) => g.area)).toEqual(NAV_AREAS);
    const total = groups.reduce((n, g) => n + g.items.length, 0);
    expect(total).toBe(NAVIGATION.filter((item) => item.phase === 'P1').length);
  });
});
