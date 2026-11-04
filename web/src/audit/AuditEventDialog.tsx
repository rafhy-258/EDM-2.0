import {
  Button,
  Dialog,
  DialogActions,
  DialogBody,
  DialogContent,
  DialogSurface,
  DialogTitle,
  Table,
  TableBody,
  TableCell,
  TableCellLayout,
  TableHeader,
  TableHeaderCell,
  TableRow,
  Text,
} from '@fluentui/react-components';
import type { ReactNode } from 'react';
import { formatKarachiDate, formatKarachiTime } from './format';
import type { AuditEvent } from './auditApi';

/** Shows one audit event: who did what, when, and the field-level changes (Part 10.1). */
export function AuditEventDialog({
  event,
  onClose,
}: {
  readonly event: AuditEvent | null;
  readonly onClose: () => void;
}) {
  return (
    <Dialog open={event !== null} onOpenChange={(_, data) => { if (!data.open) onClose(); }}>
      <DialogSurface aria-describedby="audit-event-body">
        {event ? (
          <DialogBody>
            <DialogTitle>Audit event</DialogTitle>
            <DialogContent id="audit-event-body">
              <dl style={{ display: 'grid', gridTemplateColumns: 'max-content 1fr', gap: '6px 16px', margin: 0 }}>
                <Term>Date and time</Term>
                <Value>
                  {formatKarachiDate(new Date(event.occurredAtUtc))} {formatKarachiTime(new Date(event.occurredAtUtc))} (Asia/Karachi)
                </Value>
                <Term>Actor</Term>
                <Value>
                  {event.actorName}
                  {event.actorDepartment ? `, ${event.actorDepartment}` : ''}
                  {event.actorRoles.length > 0 ? ` (${event.actorRoles.join(', ')})` : ''}
                </Value>
                <Term>Module</Term>
                <Value>{event.module}</Value>
                <Term>Action</Term>
                <Value>{event.action}</Value>
                <Term>Entity</Term>
                <Value>
                  {event.entityType}: {event.entityId}
                </Value>
                <Term>Outcome</Term>
                <Value>
                  {event.outcome}
                  {event.failureReason ? `: ${event.failureReason}` : ''}
                </Value>
                {event.remarks ? (
                  <>
                    <Term>Remarks</Term>
                    <Value>{event.remarks}</Value>
                  </>
                ) : null}
                {event.requestReference ? (
                  <>
                    <Term>Reference</Term>
                    <Value>{event.requestReference}</Value>
                  </>
                ) : null}
                {event.policyVersion !== null ? (
                  <>
                    <Term>Policy version</Term>
                    <Value>{String(event.policyVersion)}</Value>
                  </>
                ) : null}
                <Term>Correlation id</Term>
                <Value>{event.correlationId}</Value>
              </dl>

              <Text weight="semibold" as="h3" style={{ display: 'block', marginTop: 16 }}>
                Changes
              </Text>
              {event.changes && event.changes.length > 0 ? (
                <Table aria-label="Field changes" size="small">
                  <TableHeader>
                    <TableRow>
                      <TableHeaderCell>Field</TableHeaderCell>
                      <TableHeaderCell>Before</TableHeaderCell>
                      <TableHeaderCell>After</TableHeaderCell>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {event.changes.map((change) => (
                      <TableRow key={change.field}>
                        <TableCell>
                          <TableCellLayout>{change.field}</TableCellLayout>
                        </TableCell>
                        <TableCell>
                          <TableCellLayout>
                            <code>{displayValue(change.before)}</code>
                          </TableCellLayout>
                        </TableCell>
                        <TableCell>
                          <TableCellLayout>
                            <code>{displayValue(change.after)}</code>
                          </TableCellLayout>
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              ) : (
                <Text>No field changes were recorded for this event.</Text>
              )}
            </DialogContent>
            <DialogActions>
              <Button appearance="primary" onClick={onClose}>
                Close
              </Button>
            </DialogActions>
          </DialogBody>
        ) : null}
      </DialogSurface>
    </Dialog>
  );
}

function Term({ children }: { readonly children: string }) {
  return <dt style={{ fontWeight: 600 }}>{children}</dt>;
}

function Value({ children }: { readonly children: ReactNode }) {
  return <dd style={{ margin: 0, wordBreak: 'break-word' }}>{children}</dd>;
}

/** Null or missing values show as a dash. Other values are shown as JSON text. */
export function displayValue(value: unknown): string {
  if (value === null || value === undefined) return '—';
  if (typeof value === 'string') return value;
  return JSON.stringify(value);
}
