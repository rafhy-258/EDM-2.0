import type { Permission } from '../session/permissions';

/**
 * Navigation data from Master Prompt Part 14.1.
 *
 * phase: 'P1' items are built in Phase 1 and shown as placeholders in Phase 0.
 *        'P2' items are Phase 2. They are defined here so that Phase 2 can switch them on, but they
 *        are never rendered and never routed in Phase 0 or Phase 1 (Part 24.2).
 * requiredPermission: the Part 20 permission that shows the item. `null` means any signed-in user.
 *        This is for display only. The server enforces every permission.
 */
export type NavArea =
  | 'Workspace'
  | 'Expense Management'
  | 'Workflow and Approvals'
  | 'Organisation Management'
  | 'Configuration and Policies'
  | 'Identity and Access'
  | 'Reports and Analytics'
  | 'Audit and Compliance'
  | 'Integrations'
  | 'System Administration';

export interface NavItemDef {
  readonly id: string;
  readonly area: NavArea;
  readonly label: string;
  readonly route: string;
  readonly phase: 'P1' | 'P2';
  readonly requiredPermission: Permission | null;
}

export const NAV_AREAS: readonly NavArea[] = [
  'Workspace',
  'Expense Management',
  'Workflow and Approvals',
  'Organisation Management',
  'Configuration and Policies',
  'Identity and Access',
  'Reports and Analytics',
  'Audit and Compliance',
  'Integrations',
  'System Administration',
];

export const NAVIGATION: readonly NavItemDef[] = [
  // Workspace
  { id: 'overview', area: 'Workspace', label: 'Overview', route: '/workspace/overview', phase: 'P1', requiredPermission: null },
  { id: 'my-tasks', area: 'Workspace', label: 'My Tasks', route: '/workspace/my-tasks', phase: 'P1', requiredPermission: 'approval.inbox.view' },
  { id: 'my-requests', area: 'Workspace', label: 'My Requests', route: '/workspace/my-requests', phase: 'P1', requiredPermission: 'opex.request.view.own' },
  { id: 'workspace-notifications', area: 'Workspace', label: 'Notifications', route: '/workspace/notifications', phase: 'P1', requiredPermission: null },

  // Expense Management
  { id: 'create-expense', area: 'Expense Management', label: 'Create Expense', route: '/expenses/new', phase: 'P1', requiredPermission: 'opex.request.create' },
  { id: 'opex', area: 'Expense Management', label: 'OPEX', route: '/expenses/opex', phase: 'P1', requiredPermission: 'opex.request.view.own' },
  { id: 'capex', area: 'Expense Management', label: 'CAPEX', route: '/expenses/capex', phase: 'P1', requiredPermission: 'capex.request.view.own' },
  { id: 'drafts', area: 'Expense Management', label: 'Drafts', route: '/expenses/drafts', phase: 'P2', requiredPermission: 'opex.request.create' },
  { id: 'submitted-requests', area: 'Expense Management', label: 'Submitted Requests', route: '/expenses/submitted', phase: 'P1', requiredPermission: 'opex.request.view.department' },
  { id: 'expense-register', area: 'Expense Management', label: 'Expense Register', route: '/expenses/register', phase: 'P1', requiredPermission: 'report.view.own' },

  // Workflow and Approvals
  { id: 'approval-inbox', area: 'Workflow and Approvals', label: 'Approval Inbox', route: '/approvals/inbox', phase: 'P1', requiredPermission: 'approval.inbox.view' },
  { id: 'workflow-designer', area: 'Workflow and Approvals', label: 'Workflow Designer', route: '/approvals/designer', phase: 'P2', requiredPermission: 'config.matrix.manage' },
  { id: 'approval-policies', area: 'Workflow and Approvals', label: 'Approval Policies', route: '/approvals/policies', phase: 'P1', requiredPermission: 'config.matrix.view' },
  { id: 'delegation', area: 'Workflow and Approvals', label: 'Delegation', route: '/approvals/delegation', phase: 'P2', requiredPermission: 'approval.act' },
  { id: 'escalations', area: 'Workflow and Approvals', label: 'Escalations', route: '/approvals/escalations', phase: 'P2', requiredPermission: 'approval.act' },

  // Organisation Management
  { id: 'departments', area: 'Organisation Management', label: 'Departments', route: '/organisation/departments', phase: 'P1', requiredPermission: 'org.department.manage' },
  { id: 'branches', area: 'Organisation Management', label: 'Branches', route: '/organisation/branches', phase: 'P1', requiredPermission: 'org.branch.manage' },
  { id: 'business-units', area: 'Organisation Management', label: 'Business Units', route: '/organisation/business-units', phase: 'P2', requiredPermission: 'org.department.manage' },
  { id: 'positions', area: 'Organisation Management', label: 'Positions', route: '/organisation/positions', phase: 'P2', requiredPermission: 'org.user.view' },
  { id: 'hierarchy', area: 'Organisation Management', label: 'Organisational Hierarchy', route: '/organisation/hierarchy', phase: 'P1', requiredPermission: 'org.hierarchy.manage' },

  // Configuration and Policies
  { id: 'categories', area: 'Configuration and Policies', label: 'Categories', route: '/configuration/categories', phase: 'P1', requiredPermission: 'config.category.view' },
  { id: 'subcategories', area: 'Configuration and Policies', label: 'Subcategories', route: '/configuration/subcategories', phase: 'P1', requiredPermission: 'config.subcategory.manage' },
  { id: 'financial-limits', area: 'Configuration and Policies', label: 'Financial Limits', route: '/configuration/financial-limits', phase: 'P1', requiredPermission: 'config.limit.manage' },
  { id: 'approval-authorities', area: 'Configuration and Policies', label: 'Approval Authorities', route: '/configuration/approval-authorities', phase: 'P1', requiredPermission: 'config.ra.manage' },
  { id: 'opex-capex-policies', area: 'Configuration and Policies', label: 'OPEX/CAPEX Policies', route: '/configuration/policies', phase: 'P1', requiredPermission: 'config.policy.view' },
  { id: 'rule-builder', area: 'Configuration and Policies', label: 'Rule Builder', route: '/configuration/rule-builder', phase: 'P2', requiredPermission: 'config.matrix.manage' },

  // Identity and Access
  { id: 'users', area: 'Identity and Access', label: 'Users', route: '/access/users', phase: 'P1', requiredPermission: 'org.user.view' },
  { id: 'roles', area: 'Identity and Access', label: 'Roles', route: '/access/roles', phase: 'P1', requiredPermission: 'iam.role.manage' },
  { id: 'permissions', area: 'Identity and Access', label: 'Permissions', route: '/access/permissions', phase: 'P1', requiredPermission: 'iam.permission.assign' },
  { id: 'access-policies', area: 'Identity and Access', label: 'Access Policies', route: '/access/policies', phase: 'P2', requiredPermission: 'iam.role.manage' },
  { id: 'sso', area: 'Identity and Access', label: 'SSO', route: '/access/sso', phase: 'P1', requiredPermission: 'iam.sso.manage' },
  { id: 'mfa', area: 'Identity and Access', label: 'MFA', route: '/access/mfa', phase: 'P1', requiredPermission: 'iam.mfa.manage' },

  // Reports and Analytics
  { id: 'expense-reports', area: 'Reports and Analytics', label: 'Expense Reports', route: '/reports/expenses', phase: 'P1', requiredPermission: 'report.view.own' },
  { id: 'approval-performance', area: 'Reports and Analytics', label: 'Approval Performance', route: '/reports/approval-performance', phase: 'P2', requiredPermission: 'report.view.all' },
  { id: 'department-spending', area: 'Reports and Analytics', label: 'Department Spending', route: '/reports/department-spending', phase: 'P1', requiredPermission: 'report.view.department' },
  { id: 'audit-reports', area: 'Reports and Analytics', label: 'Audit Reports', route: '/reports/audit', phase: 'P1', requiredPermission: 'audit.view' },
  { id: 'exports', area: 'Reports and Analytics', label: 'Exports', route: '/reports/exports', phase: 'P1', requiredPermission: 'report.export' },

  // Audit and Compliance
  { id: 'activity-logs', area: 'Audit and Compliance', label: 'Activity Logs', route: '/audit/activity', phase: 'P1', requiredPermission: 'audit.view' },
  { id: 'approval-history', area: 'Audit and Compliance', label: 'Approval History', route: '/audit/approval-history', phase: 'P1', requiredPermission: 'audit.view' },
  { id: 'policy-changes', area: 'Audit and Compliance', label: 'Policy Changes', route: '/audit/policy-changes', phase: 'P1', requiredPermission: 'config.policy.view' },
  { id: 'security-events', area: 'Audit and Compliance', label: 'Security Events', route: '/audit/security', phase: 'P1', requiredPermission: 'audit.security.view' },
  { id: 'audit-exports', area: 'Audit and Compliance', label: 'Audit Exports', route: '/audit/exports', phase: 'P1', requiredPermission: 'audit.export' },

  // Integrations (only identity providers are P1)
  { id: 'erp', area: 'Integrations', label: 'ERP', route: '/integrations/erp', phase: 'P2', requiredPermission: 'integration.manage' },
  { id: 'hrms', area: 'Integrations', label: 'HRMS', route: '/integrations/hrms', phase: 'P2', requiredPermission: 'integration.manage' },
  { id: 'financial-systems', area: 'Integrations', label: 'Financial Systems', route: '/integrations/financial-systems', phase: 'P2', requiredPermission: 'integration.manage' },
  { id: 'external-apis', area: 'Integrations', label: 'External APIs', route: '/integrations/external-apis', phase: 'P2', requiredPermission: 'integration.manage' },
  { id: 'webhooks', area: 'Integrations', label: 'Webhooks', route: '/integrations/webhooks', phase: 'P2', requiredPermission: 'integration.manage' },
  { id: 'identity-providers', area: 'Integrations', label: 'Identity Providers', route: '/integrations/identity-providers', phase: 'P1', requiredPermission: 'iam.sso.manage' },

  // System Administration
  { id: 'tenant-settings', area: 'System Administration', label: 'Tenant Settings', route: '/system/tenant', phase: 'P2', requiredPermission: 'settings.manage' },
  { id: 'general-settings', area: 'System Administration', label: 'General Settings', route: '/system/general', phase: 'P1', requiredPermission: 'settings.manage' },
  { id: 'system-notifications', area: 'System Administration', label: 'Notifications', route: '/system/notifications', phase: 'P1', requiredPermission: 'notification.template.manage' },
  { id: 'localisation', area: 'System Administration', label: 'Localisation', route: '/system/localisation', phase: 'P1', requiredPermission: 'settings.manage' },
  { id: 'data-retention', area: 'System Administration', label: 'Data Retention', route: '/system/retention', phase: 'P1', requiredPermission: 'retention.manage' },
  { id: 'system-health', area: 'System Administration', label: 'System Health', route: '/system/health', phase: 'P1', requiredPermission: 'health.view' },
];
