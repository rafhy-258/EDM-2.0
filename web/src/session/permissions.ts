/**
 * Permission catalogue, mirrored from Master Prompt Part 20.1 and docs/permission-matrix.md.
 *
 * The server is the source of truth. These strings are used by the UI only to decide what to
 * show. A permission check in the UI is never a security control (ADR-0007).
 * If Part 20 changes, change this list through a Change Request.
 */
export const PERMISSIONS = [
  // Requests
  'opex.request.create',
  'opex.request.view.own',
  'opex.request.view.department',
  'opex.request.view.all',
  'opex.request.edit.returned',
  'capex.request.create',
  'capex.request.view.own',
  'capex.request.view.department',
  'capex.request.view.all',
  'capex.request.edit.returned',
  // Approvals
  'approval.inbox.view',
  'approval.act',
  'approval.view.assigned',
  // Configuration
  'config.category.view',
  'config.category.manage',
  'config.subcategory.manage',
  'config.matrix.view',
  'config.matrix.manage',
  'config.matrix.approve',
  'config.ec.pool.manage',
  'config.ec.level.manage',
  'config.ra.manage',
  'config.limit.manage',
  'config.currency.manage',
  'config.capex.eligibility.manage',
  'config.actiontags.manage',
  'config.policy.view',
  // Organisation
  'org.department.manage',
  'org.branch.manage',
  'org.hierarchy.manage',
  'org.user.view',
  'org.user.manage',
  'org.sync.run',
  // Identity and access
  'iam.role.manage',
  'iam.permission.assign',
  'iam.sso.manage',
  'iam.mfa.manage',
  // Reports
  'report.view.own',
  'report.view.department',
  'report.view.all',
  'report.export',
  // Audit
  'audit.view',
  'audit.export',
  'audit.security.view',
  // System
  'settings.manage',
  'notification.template.manage',
  'retention.manage',
  'health.view',
  'integration.manage',
] as const;

export type Permission = (typeof PERMISSIONS)[number];
