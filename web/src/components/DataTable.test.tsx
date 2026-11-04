import { fireEvent, render, screen, within } from '@testing-library/react';
import type { ColumnDef } from '@tanstack/react-table';
import { FluentProvider, webLightTheme } from '@fluentui/react-components';
import { describe, expect, it } from 'vitest';
import { DataTable } from './DataTable';

interface Row {
  readonly reference: string;
  readonly amount: number;
}

const columns: ColumnDef<Row, unknown>[] = [
  { accessorKey: 'reference', header: 'Reference' },
  { accessorKey: 'amount', header: 'Amount' },
];

function renderTable(props: Partial<Parameters<typeof DataTable<Row>>[0]> = {}) {
  return render(
    <FluentProvider theme={webLightTheme}>
      <DataTable<Row>
        caption="Requests"
        data={[
          { reference: 'OPEX-2026-000002', amount: 200 },
          { reference: 'OPEX-2026-000001', amount: 100 },
        ]}
        columns={columns}
        emptyTitle="No requests yet"
        {...props}
      />
    </FluentProvider>,
  );
}

describe('DataTable', () => {
  it('renders rows from the data', () => {
    renderTable();
    const table = screen.getByRole('table', { name: 'Requests' });
    expect(within(table).getByText('OPEX-2026-000002')).toBeInTheDocument();
  });

  it('sorts when a sortable header is activated and reports the direction', () => {
    renderTable();
    const header = screen.getByRole('columnheader', { name: /Reference/ });
    fireEvent.click(header);
    const rows = screen.getAllByRole('row').slice(1);
    expect(rows[0]).toHaveTextContent('OPEX-2026-000001');
    expect(header).toHaveAttribute('aria-sort', 'ascending');
  });

  it('shows the empty state when there is no data', () => {
    renderTable({ data: [] });
    expect(screen.getByRole('heading', { name: 'No requests yet' })).toBeInTheDocument();
  });

  it('shows the loading state', () => {
    renderTable({ isLoading: true, data: undefined });
    expect(screen.getByRole('status')).toBeInTheDocument();
  });

  it('shows the error state with a retry action that calls back', () => {
    let retried = 0;
    renderTable({ error: new Error('boom'), data: undefined, onRetry: () => retried++ });
    expect(screen.getByRole('alert')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Try again' }));
    expect(retried).toBe(1);
  });

  it('pages when there are more rows than the page size', () => {
    const many = Array.from({ length: 30 }, (_, i) => ({ reference: `R-${String(i).padStart(3, '0')}`, amount: i }));
    renderTable({ data: many, pageSize: 25 });
    expect(screen.getByText('Page 1 of 2')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Next' }));
    expect(screen.getByText('Page 2 of 2')).toBeInTheDocument();
  });
});
