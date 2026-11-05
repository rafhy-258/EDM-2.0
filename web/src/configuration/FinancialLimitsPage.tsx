import { Badge, Button, Tab, TabList, Text, Title1, type SelectTabData, type SelectTabEvent } from '@fluentui/react-components';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import type { ColumnDef } from '@tanstack/react-table';
import { useMemo, useState } from 'react';
import { formatKarachiDate } from '../audit/format';
import { ApiError } from '../api/apiError';
import { ConfirmDialog } from '../components/ConfirmDialog';
import { DataTable } from '../components/DataTable';
import { useSession } from '../session/session';
import { DeptHeadLimitDialog } from './DeptHeadLimitDialog';
import {
  deactivateDeptHeadLimit,
  getDeptHeadLimit,
  type DeptHeadLimitRecord,
  type LimitNature,
} from './configurationApi';
import { formatPkr, messageFor } from './configurationValidation';
import styles from './ConfigurationPage.module.css';

/** Status of one history row, for the table. */
function rowStatus(row: DeptHeadLimitRecord, now: number): string {
  if (!row.isActive) {
    return 'Ended';
  }
  return Date.parse(row.effectiveFrom) <= now ? 'Active' : 'Scheduled';
}

/**
 * Financial limits: the department-head limit for OPEX and CAPEX (Master Prompt 5.5). A request at or below the limit
 * goes to the Department Head only. Setting a new limit keeps the old one as history. Ending the limit means the
 * seeded default applies. Changes apply when saved: maker-checker is not built yet.
 */
export function FinancialLimitsPage() {
  const { hasPermission } = useSession();
  const canManage = hasPermission('config.limit.manage');
  const queryClient = useQueryClient();
  const [nature, setNature] = useState<LimitNature>('OPEX');
  const [editing, setEditing] = useState(false);
  const [ending, setEnding] = useState(false);
  const [actionError, setActionError] = useState<string | null>(null);

  const query = useQuery({
    queryKey: ['configuration', 'dept-head-limit', nature],
    queryFn: () => getDeptHeadLimit(nature),
    retry: (failureCount, error) => !(error instanceof ApiError) && failureCount < 2,
  });
  const summary = query.data;

  const end = useMutation({
    mutationFn: async () => {
      if (!summary?.active) {
        throw new Error('There is no active limit to end.');
      }
      await deactivateDeptHeadLimit(nature, summary.active.rowVersion);
    },
    onSuccess: async () => {
      setEnding(false);
      setActionError(null);
      await queryClient.invalidateQueries({ queryKey: ['configuration', 'dept-head-limit'] });
    },
    onError: (error) => {
      setEnding(false);
      setActionError(messageFor(error));
    },
  });

  const columns = useMemo<ColumnDef<DeptHeadLimitRecord, unknown>[]>(
    () => [
      {
        id: 'amount',
        header: 'Limit',
        accessorFn: (row) => row.amountPkr,
        cell: ({ row }) => formatPkr(row.original.amountPkr),
      },
      {
        id: 'from',
        header: 'From',
        accessorFn: (row) => row.effectiveFrom,
        cell: ({ row }) => formatKarachiDate(new Date(row.original.effectiveFrom)),
      },
      {
        id: 'to',
        header: 'To',
        accessorFn: (row) => row.effectiveTo ?? '',
        cell: ({ row }) => (row.original.effectiveTo ? formatKarachiDate(new Date(row.original.effectiveTo)) : 'Open'),
      },
      {
        id: 'status',
        header: 'Status',
        accessorFn: (row) => rowStatus(row, Date.now()),
        cell: ({ row }) => {
          const label = rowStatus(row.original, Date.now());
          return (
            <Badge appearance="filled" color={label === 'Active' ? 'brand' : 'subtle'} shape="rounded">
              {label}
            </Badge>
          );
        },
      },
      {
        id: 'description',
        header: 'Description',
        accessorFn: (row) => row.description ?? '',
        cell: ({ row }) => row.original.description ?? '',
      },
      {
        id: 'changed',
        header: 'Last changed',
        accessorFn: (row) => row.updatedAtUtc,
        cell: ({ row }) => formatKarachiDate(new Date(row.original.updatedAtUtc)),
      },
    ],
    [],
  );

  function statusExplanation(): string {
    if (!summary) {
      return '';
    }
    if (summary.status === 'Active') {
      return 'Applies now.';
    }
    if (summary.status === 'Scheduled' && summary.active) {
      return `Starts ${formatKarachiDate(new Date(summary.active.effectiveFrom))}. Until then the limit above applies.`;
    }
    return 'No active limit. The default applies.';
  }

  return (
    <section aria-labelledby="financial-limits-title" className={styles.page}>
      <div className={styles.header}>
        <Title1 id="financial-limits-title" as="h1">
          Financial Limits
        </Title1>
        <Badge appearance="outline" color="informative">
          Configuration and Policies
        </Badge>
      </div>
      <Text>
        Requests at or below the department-head limit go to the Department Head only. OPEX and CAPEX each have their own
        limit. Setting a new limit keeps the old one as history.
      </Text>
      <Text className={styles.muted}>
        Changes apply as soon as they are saved. A second approver is not required yet.
      </Text>

      <TabList
        selectedValue={nature}
        onTabSelect={(_: SelectTabEvent, data: SelectTabData) => setNature(String(data.value) === 'CAPEX' ? 'CAPEX' : 'OPEX')}
        aria-label="Limit type"
      >
        <Tab value="OPEX">OPEX</Tab>
        <Tab value="CAPEX">CAPEX</Tab>
      </TabList>

      {summary ? (
        <div className={styles.cards}>
          <div className={styles.card}>
            <Text className={styles.muted}>Current limit</Text>
            <Text className={styles.cardValue}>{formatPkr(summary.currentAmountPkr)}</Text>
            <Text className={styles.muted}>{summary.isDefault ? 'System default. No limit is set.' : 'Set by an administrator.'}</Text>
          </div>
          <div className={styles.card}>
            <Text className={styles.muted}>Status</Text>
            <Badge appearance="filled" color={summary.status === 'Active' ? 'brand' : 'subtle'} shape="rounded">
              {summary.status}
            </Badge>
            <Text className={styles.muted}>{statusExplanation()}</Text>
          </div>
          <div className={styles.card}>
            <Text className={styles.muted}>Last changed</Text>
            <Text className={styles.cardValue}>
              {summary.lastChangedAtUtc ? formatKarachiDate(new Date(summary.lastChangedAtUtc)) : 'Never'}
            </Text>
          </div>
        </div>
      ) : null}

      <div className={styles.toolbar}>
        <Text className={styles.muted}>
          {summary ? `Default if no limit is set: ${formatPkr(summary.defaultAmountPkr)}` : ''}
        </Text>
        {canManage ? (
          <div className={styles.actions}>
            <Button appearance="secondary" disabled={!summary?.active || end.isPending} onClick={() => setEnding(true)}>
              End current limit
            </Button>
            <Button appearance="primary" disabled={!summary} onClick={() => setEditing(true)}>
              Set new limit
            </Button>
          </div>
        ) : null}
      </div>

      {actionError ? (
        <Text role="alert" className={styles.error}>
          {actionError}
        </Text>
      ) : null}

      <DataTable
        caption={`${nature} limit history`}
        data={summary?.history}
        columns={columns}
        isLoading={query.isPending}
        error={query.error}
        onRetry={() => void query.refetch()}
        emptyTitle="No limit has been set"
        emptyDescription={canManage ? 'The default applies until you set a limit.' : 'The default applies until a limit is set.'}
      />

      {editing && summary ? (
        <DeptHeadLimitDialog key={nature} nature={nature} summary={summary} onClose={() => setEditing(false)} />
      ) : null}

      <ConfirmDialog
        open={ending}
        title="End the current limit?"
        description={
          <Text>
            {summary?.active ? formatPkr(summary.active.amountPkr) : 'The limit'} will stop applying now. The default of{' '}
            {summary ? formatPkr(summary.defaultAmountPkr) : ''} applies until a new limit is set. The record stays in the
            history.
          </Text>
        }
        confirmLabel="End limit"
        destructive
        busy={end.isPending}
        onConfirm={() => end.mutate()}
        onCancel={() => setEnding(false)}
      />
    </section>
  );
}
