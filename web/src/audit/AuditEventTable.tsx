import {
  Badge,
  Button,
  Table,
  TableBody,
  TableCell,
  TableCellLayout,
  TableHeader,
  TableHeaderCell,
  TableRow,
} from '@fluentui/react-components';
import {
  flexRender,
  getCoreRowModel,
  useReactTable,
  type ColumnDef,
} from '@tanstack/react-table';
import { useMemo } from 'react';
import { formatKarachiDate, formatKarachiTime } from './format';
import type { AuditEvent } from './auditApi';

/**
 * Read-only table of audit events for one page. Paging is done by the server, so the table does
 * not page or sort the rows itself. Outcome is shown as text, never colour alone.
 */
export function AuditEventTable({
  caption,
  events,
  onShowDetails,
}: {
  readonly caption: string;
  readonly events: readonly AuditEvent[];
  readonly onShowDetails: (event: AuditEvent) => void;
}) {
  const columns: ColumnDef<AuditEvent, unknown>[] = [
    {
      id: 'date',
      header: 'Date (PKT)',
      accessorFn: (row) => formatKarachiDate(new Date(row.occurredAtUtc)),
    },
    {
      id: 'time',
      header: 'Time (PKT)',
      accessorFn: (row) => formatKarachiTime(new Date(row.occurredAtUtc)),
    },
    { id: 'actor', header: 'Actor', accessorFn: (row) => row.actorName },
    { id: 'module', header: 'Module', accessorFn: (row) => row.module },
    { id: 'action', header: 'Action', accessorFn: (row) => row.action },
    {
      id: 'entity',
      header: 'Entity',
      accessorFn: (row) => `${row.entityType}: ${row.entityId}`,
    },
    {
      id: 'outcome',
      header: 'Outcome',
      accessorFn: (row) => row.outcome,
      cell: ({ row }) => (
        <Badge appearance="outline" color={row.original.outcome === 'Failure' ? 'danger' : 'success'}>
          {row.original.outcome}
        </Badge>
      ),
    },
    {
      id: 'details',
      header: 'Details',
      enableSorting: false,
      cell: ({ row }) => (
        <Button
          appearance="subtle"
          size="small"
          aria-label={`View details of ${row.original.action} on ${row.original.entityType} ${row.original.entityId} at ${formatKarachiTime(new Date(row.original.occurredAtUtc))}`}
          onClick={() => onShowDetails(row.original)}
        >
          View
        </Button>
      ),
    },
  ];

  // Keep the same row array while the events are the same. A new array on every render makes the table loop.
  const tableData = useMemo(() => [...events], [events]);

  // TanStack Table returns functions that cannot be memoised safely by React Compiler, so the
  // hook result is used directly in this component (same as DataTable).
  // eslint-disable-next-line react-hooks/incompatible-library
  const table = useReactTable({
    data: tableData,
    columns,
    getCoreRowModel: getCoreRowModel(),
  });

  return (
    <Table aria-label={caption} size="small">
      <TableHeader>
        <TableRow>
          {table.getHeaderGroups()[0]?.headers.map((header) => (
            <TableHeaderCell key={header.id}>
              <TableCellLayout>{flexRender(header.column.columnDef.header, header.getContext())}</TableCellLayout>
            </TableHeaderCell>
          ))}
        </TableRow>
      </TableHeader>
      <TableBody>
        {table.getRowModel().rows.map((row) => (
          <TableRow key={row.id}>
            {row.getVisibleCells().map((cell) => (
              <TableCell key={cell.id}>
                <TableCellLayout>{flexRender(cell.column.columnDef.cell, cell.getContext())}</TableCellLayout>
              </TableCell>
            ))}
          </TableRow>
        ))}
      </TableBody>
    </Table>
  );
}
