import { FluentProvider, webLightTheme } from '@fluentui/react-components';
import { render, screen } from '@testing-library/react';
import type { ColumnDef } from '@tanstack/react-table';
import { describe, expect, it } from 'vitest';
import { ApiError } from '../api/apiError';
import { DataTable } from './DataTable';

interface Row {
  readonly id: string;
  readonly name: string;
}

const columns: ColumnDef<Row, unknown>[] = [{ id: 'name', header: 'Name', accessorFn: (row) => row.name }];

function view(data: readonly Row[], error?: unknown) {
  return (
    <FluentProvider theme={webLightTheme}>
      <DataTable caption="Things" data={data} columns={columns} emptyTitle="None" error={error} />
    </FluentProvider>
  );
}

describe('DataTable', () => {
  it('keeps showing its rows when the parent re-renders with new array objects', () => {
    const rows: Row[] = [{ id: '1', name: 'Finance Expenses' }];
    const { rerender } = render(view(rows));

    // Each re-render passes a new array with the same rows. The table must settle, not loop.
    for (let i = 0; i < 5; i += 1) {
      rerender(view([...rows]));
    }

    expect(screen.getByText('Finance Expenses')).toBeInTheDocument();
  });

  it('shows the API error message instead of masking it with a generic state', () => {
    render(view([], new ApiError(401, 'EDM-AUTH-001', 'Please sign in.')));

    expect(screen.getByRole('alert')).toHaveTextContent('Please sign in.');
  });
});
