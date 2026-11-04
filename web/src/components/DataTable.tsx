import {
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
  getPaginationRowModel,
  getSortedRowModel,
  useReactTable,
  type ColumnDef,
  type SortingState,
} from '@tanstack/react-table';
import { useMemo, useState } from 'react';
import { EmptyState, ErrorState, LoadingState } from './StateViews';

/**
 * Data table primitive (ADR-0003): TanStack Table v8 logic rendered with Fluent table primitives.
 * Phase 0 covers client-side sorting and paging for small sets. Server-side paging (Part 21.1)
 * is added with the first list screen that needs it.
 */
export interface DataTableProps<T> {
  readonly caption: string;
  readonly data: readonly T[] | undefined;
  readonly columns: ColumnDef<T, unknown>[];
  readonly isLoading?: boolean;
  readonly error?: unknown;
  readonly onRetry?: () => void;
  readonly emptyTitle: string;
  readonly emptyDescription?: string;
  readonly pageSize?: number;
}

export function DataTable<T>({
  caption,
  data,
  columns,
  isLoading = false,
  error,
  onRetry,
  emptyTitle,
  emptyDescription,
  pageSize = 25,
}: DataTableProps<T>) {
  const [sorting, setSorting] = useState<SortingState>([]);
  const [pageIndex, setPageIndex] = useState(0);

  // A new array on every render makes TanStack Table reset the page on every render, which loops and freezes the
  // page. Keep the same array while the data is the same.
  const tableData = useMemo(() => [...(data ?? [])], [data]);

  // TanStack Table returns functions that cannot be memoised safely by React Compiler, so the
  // hook result is used directly in this component.
  // eslint-disable-next-line react-hooks/incompatible-library
  const table = useReactTable({
    data: tableData,
    columns,
    autoResetPageIndex: false,
    state: { sorting, pagination: { pageIndex, pageSize } },
    onSortingChange: setSorting,
    getCoreRowModel: getCoreRowModel(),
    getSortedRowModel: getSortedRowModel(),
    getPaginationRowModel: getPaginationRowModel(),
  });

  if (isLoading) return <LoadingState label={`Loading ${caption}`} />;
  if (error) return <ErrorState onRetry={onRetry} />;
  if (!data || data.length === 0) {
    return <EmptyState title={emptyTitle} description={emptyDescription} />;
  }

  const pageCount = table.getPageCount();
  const rows = table.getRowModel().rows;

  return (
    <div>
      <Table aria-label={caption} size="small">
        <TableHeader>
          <TableRow>
            {table.getHeaderGroups()[0]?.headers.map((header) => {
              const sortDirection = header.column.getIsSorted();
              const canSort = header.column.getCanSort();
              return (
                <TableHeaderCell
                  key={header.id}
                  sortDirection={sortDirection === false ? undefined : sortDirection === 'asc' ? 'ascending' : 'descending'}
                  onClick={canSort ? header.column.getToggleSortingHandler() : undefined}
                  aria-sort={
                    sortDirection === false ? (canSort ? 'none' : undefined) : sortDirection === 'asc' ? 'ascending' : 'descending'
                  }
                >
                  <TableCellLayout>
                    {header.isPlaceholder ? null : flexRender(header.column.columnDef.header, header.getContext())}
                  </TableCellLayout>
                </TableHeaderCell>
              );
            })}
          </TableRow>
        </TableHeader>
        <TableBody>
          {rows.map((row) => (
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
      {pageCount > 1 ? (
        <nav aria-label={`${caption} pagination`} style={{ display: 'flex', gap: 8, alignItems: 'center', marginTop: 12 }}>
          <Button
            appearance="secondary"
            disabled={pageIndex === 0}
            onClick={() => setPageIndex((i) => Math.max(0, i - 1))}
          >
            Previous
          </Button>
          <span aria-live="polite">
            Page {pageIndex + 1} of {pageCount}
          </span>
          <Button
            appearance="secondary"
            disabled={pageIndex >= pageCount - 1}
            onClick={() => setPageIndex((i) => Math.min(pageCount - 1, i + 1))}
          >
            Next
          </Button>
        </nav>
      ) : null}
    </div>
  );
}
