import { PERMISSIONS, type Permission } from './permissions';

/**
 * Development-only role fixtures that mirror Master Prompt Part 20.2.
 *
 * Used only when `import.meta.env.DEV` is true, so a developer can see the menu change by role.
 * The seeded roles on the server are the source of truth. These fixtures are not a security control.
 */
export const ROLE_KEYS = [
  'REQUESTER',
  'DEPARTMENT_HEAD',
  'EDM_ADMINISTRATOR',
  'CONFIGURATION_APPROVER',
  'AUDITOR',
  'FINANCE_VIEWER',
  'SYSTEM_ADMINISTRATOR',
] as const;

export type RoleKey = (typeof ROLE_KEYS)[number];

export const ROLE_LABELS: Record<RoleKey, string> = {
  REQUESTER: 'Requester',
  DEPARTMENT_HEAD: 'Department Head (hierarchy)',
  EDM_ADMINISTRATOR: 'EDM Administrator',
  CONFIGURATION_APPROVER: 'Configuration Approver',
  AUDITOR: 'Auditor / Compliance',
  FINANCE_VIEWER: 'Finance Viewer',
  SYSTEM_ADMINISTRATOR: 'System Administrator',
};

const REQUESTER: Permission[] = [
  'opex.request.create',
  'opex.request.view.own',
  'opex.request.edit.returned',
  'capex.request.create',
  'capex.request.view.own',
  'capex.request.edit.returned',
  'approval.inbox.view',
  'report.view.own',
];

/** Department Head is not stored as a role. It is granted through the hierarchy (Part 20.2). */
const DEPARTMENT_HEAD: Permission[] = [
  ...REQUESTER,
  'opex.request.view.department',
  'capex.request.view.department',
  'report.view.department',
  'approval.act',
];

const CONFIG_MANAGE_EXCEPT_APPROVE: Permission[] = PERMISSIONS.filter(
  (p) => p.startsWith('config.') && p !== 'config.matrix.approve',
);

export const ROLE_PERMISSIONS: Record<RoleKey, readonly Permission[]> = {
  REQUESTER,
  DEPARTMENT_HEAD,
  EDM_ADMINISTRATOR: CONFIG_MANAGE_EXCEPT_APPROVE,
  CONFIGURATION_APPROVER: ['config.matrix.view', 'config.matrix.approve', 'config.policy.view'],
  AUDITOR: [
    'opex.request.view.all',
    'capex.request.view.all',
    'report.view.all',
    'report.export',
    'audit.view',
    'audit.export',
    'audit.security.view',
    'config.policy.view',
  ],
  FINANCE_VIEWER: [
    'opex.request.view.all',
    'capex.request.view.all',
    'report.view.all',
    'report.export',
  ],
  SYSTEM_ADMINISTRATOR: [
    // PROPOSED DEFAULT (open question 1A-8 and 0-2): Part 20.2 grants no role the department, branch,
    // hierarchy or user-view permissions, so those screens would be unreachable. Until the product
    // owner confirms, System Administrator holds them for read and maintenance of organisation data.
    'org.department.manage',
    'org.branch.manage',
    'org.hierarchy.manage',
    'org.user.view',
    'org.user.manage',
    'org.sync.run',
    'iam.role.manage',
    'iam.permission.assign',
    'iam.sso.manage',
    'iam.mfa.manage',
    'settings.manage',
    'notification.template.manage',
    'retention.manage',
    'health.view',
    'integration.manage',
  ],
};
