import { FluentProvider, webLightTheme } from '@fluentui/react-components';
import { render, screen, act } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { AuditEventTable } from './AuditEventTable';
import type { AuditEvent } from './auditApi';

const event = {
  id: 'e1',
  occurredAtUtc: '2026-11-04T05:00:00Z',
  actorUserId: null,
  actorName: 'Admin',
  actorDepartment: null,
  actorRoles: [],
  correlationId: 'c1',
  module: 'Configuration',
  entityType: 'Category',
  entityId: 'x',
  action: 'Created',
  outcome: 'Success',
  failureReason: null,
  remarks: null,
  requestReference: null,
  policyVersion: null,
} as unknown as AuditEvent;

describe('AuditEventTable', () => {
  it('settles when the parent re-renders with new array objects (no freeze)', async () => {
    const events = [event];
    const onShow = () => undefined;
    const view = (list: AuditEvent[]) => (
      <FluentProvider theme={webLightTheme}>
        <AuditEventTable caption="Audit events" events={list} onShowDetails={onShow} />
      </FluentProvider>
    );
    const { rerender } = render(view(events));

    for (let i = 0; i < 3; i += 1) {
      await act(async () => {
        rerender(view([...events]));
      });
    }

    expect(screen.getByText('Created')).toBeInTheDocument();
  });
});
