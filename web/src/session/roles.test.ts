import { describe, expect, it } from 'vitest';
import { PERMISSIONS, type Permission } from './permissions';
import { ROLE_KEYS, ROLE_PERMISSIONS, type RoleKey } from './roles';

const holders = (permission: Permission): RoleKey[] =>
  ROLE_KEYS.filter((role) => ROLE_PERMISSIONS[role].includes(permission));

describe('role fixtures (Part 20.2)', () => {
  it('defines the seven fixture roles: the six of Part 20.2 plus Department Head from the hierarchy', () => {
    expect(ROLE_KEYS).toEqual([
      'REQUESTER',
      'DEPARTMENT_HEAD',
      'EDM_ADMINISTRATOR',
      'CONFIGURATION_APPROVER',
      'AUDITOR',
      'FINANCE_VIEWER',
      'SYSTEM_ADMINISTRATOR',
    ]);
  });

  it('only uses catalogue permissions', () => {
    const catalogue = new Set<string>(PERMISSIONS);
    for (const role of ROLE_KEYS) {
      for (const p of ROLE_PERMISSIONS[role]) {
        expect(catalogue.has(p), `${role}: ${p}`).toBe(true);
      }
    }
  });

  it('gives no role approval power on the matrix except the Configuration Approver (maker-checker)', () => {
    expect(holders('config.matrix.approve')).toEqual(['CONFIGURATION_APPROVER']);
  });

  it('keeps the EDM Administrator away from matrix approval and the department and hierarchy screens', () => {
    const admin = ROLE_PERMISSIONS.EDM_ADMINISTRATOR;
    expect(admin).not.toContain('config.matrix.approve');
    expect(admin).not.toContain('org.department.manage');
    expect(admin).not.toContain('org.branch.manage');
    expect(admin).not.toContain('org.hierarchy.manage');
    expect(admin).not.toContain('org.user.view');
    expect(admin).toContain('config.matrix.manage');
  });

  it('gives the System Administrator no approval, no config approval and no audit alteration', () => {
    const sys = ROLE_PERMISSIONS.SYSTEM_ADMINISTRATOR;
    expect(sys.some((p) => p.startsWith('approval.'))).toBe(false);
    expect(sys).not.toContain('config.matrix.approve');
    expect(sys.some((p) => p.startsWith('audit.'))).toBe(false);
    expect(sys).toContain('iam.role.manage');
  });

  it('keeps the Auditor read-only: no manage permissions and no approval', () => {
    const auditor = ROLE_PERMISSIONS.AUDITOR;
    expect(auditor.some((p) => p.endsWith('.manage'))).toBe(false);
    expect(auditor.some((p) => p.startsWith('approval.'))).toBe(false);
    expect(auditor).toContain('audit.export');
  });

  it('gives the Department Head department scope and approval.act, without any manage rights', () => {
    const dh = ROLE_PERMISSIONS.DEPARTMENT_HEAD;
    expect(dh).toContain('opex.request.view.department');
    expect(dh).toContain('approval.act');
    expect(dh.some((p) => p.endsWith('.manage'))).toBe(false);
    expect(dh).not.toContain('opex.request.view.all');
  });

  it('gives the Requester only own-scope request rights, never view.all', () => {
    const requester = ROLE_PERMISSIONS.REQUESTER;
    expect(requester).not.toContain('opex.request.view.all');
    expect(requester).not.toContain('capex.request.view.all');
    expect(requester).toContain('opex.request.create');
  });

  it('gives Finance Viewer reporting and registers but no configuration or audit rights', () => {
    const fv = ROLE_PERMISSIONS.FINANCE_VIEWER;
    expect(fv.some((p) => p.startsWith('config.'))).toBe(false);
    expect(fv.some((p) => p.startsWith('audit.'))).toBe(false);
    expect(fv).toContain('report.view.all');
  });
});
