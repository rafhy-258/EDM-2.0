import { Badge, Text, Title1 } from '@fluentui/react-components';
import type { NavItemDef } from '../navigation/navigation';
import { EmptyState } from '../components/StateViews';

/**
 * Placeholder for Phase 1 screens that are not built yet. It states the phase and the area
 * so a reviewer can see exactly what is pending.
 */
export function PlaceholderPage({ item }: { readonly item: NavItemDef }) {
  return (
    <section aria-labelledby="page-title" style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
      <div style={{ display: 'flex', gap: 12, alignItems: 'center', flexWrap: 'wrap' }}>
        <Title1 id="page-title" as="h1">
          {item.label}
        </Title1>
        <Badge appearance="outline" color="informative">
          {item.area}
        </Badge>
        <Badge appearance="outline" color="subtle">
          Built in {phaseLabel(item)}
        </Badge>
      </div>
      <EmptyState
        title="This screen is not built yet"
        description="Phase 0 provides the shell only. The screen is delivered in its build phase, as set out in the master prompt."
      />
      <Text size={200}>Route: {item.route}</Text>
    </section>
  );
}

function phaseLabel(item: NavItemDef): string {
  // Phase 1 items are assigned to the sub-phase that builds them. This mapping follows Part 15.
  const map: Record<string, string> = {
    'create-expense': 'Phase 1C',
    opex: 'Phase 1C',
    capex: 'Phase 1D',
    'submitted-requests': 'Phase 1C',
    'approval-inbox': 'Phase 1C',
    'my-requests': 'Phase 1C',
    'my-tasks': 'Phase 1C',
    categories: 'Phase 1A',
    subcategories: 'Phase 1A',
    'financial-limits': 'Phase 1A',
    'approval-authorities': 'Phase 1A',
    'approval-policies': 'Phase 1A',
    'opex-capex-policies': 'Phase 1A',
    departments: 'Phase 0',
    branches: 'Phase 0',
    hierarchy: 'Phase 0',
    users: 'Phase 0',
    roles: 'Phase 0',
    permissions: 'Phase 0',
    sso: 'Phase 0',
    mfa: 'Phase 0',
    'identity-providers': 'Phase 0',
    'activity-logs': 'Phase 0',
    'approval-history': 'Phase 1C',
    'policy-changes': 'Phase 1A',
    'security-events': 'Phase 0',
    'audit-exports': 'Phase 0',
    'audit-reports': 'Phase 1E',
    'expense-reports': 'Phase 1E',
    'department-spending': 'Phase 1E',
    exports: 'Phase 1E',
    'expense-register': 'Phase 1E',
    'system-health': 'Phase 0',
    'general-settings': 'Phase 0',
    'system-notifications': 'Phase 0',
    localisation: 'Phase 0',
    'data-retention': 'Phase 1E',
    overview: 'Phase 1E',
    'workspace-notifications': 'Phase 1C',
  };
  return map[item.id] ?? 'Phase 1';
}
