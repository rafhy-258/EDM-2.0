import { Badge, Button, Switch, Text, Title1 } from '@fluentui/react-components';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import type { ColumnDef } from '@tanstack/react-table';
import { useMemo, useState } from 'react';
import { formatKarachiDate } from '../audit/format';
import { ApiError } from '../api/apiError';
import { ConfirmDialog } from '../components/ConfirmDialog';
import { DataTable } from '../components/DataTable';
import styles from '../configuration/ConfigurationPage.module.css';
import { messageFor } from '../configuration/configurationValidation';
import { useSession } from '../session/session';
import { DepartmentDialog } from './DepartmentDialog';
import { listDepartments, updateDepartment, type DepartmentRecord } from './organisationApi';

/**
 * Departments (Master Prompt Part 14.1). A department is deactivated, not deleted. It cannot be deactivated while
 * people in it are active. Every change is audited by the server.
 */
export function DepartmentsPage() {
  const { hasPermission } = useSession();
  const canManage = hasPermission('org.department.manage');
  const queryClient = useQueryClient();
  const [includeInactive, setIncludeInactive] = useState(false);
  const [editing, setEditing] = useState<DepartmentRecord | 'new' | null>(null);
  const [toggling, setToggling] = useState<DepartmentRecord | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);

  const query = useQuery({
    queryKey: ['organisation', 'departments', includeInactive],
    queryFn: () => listDepartments(includeInactive),
    retry: (failureCount, error) => !(error instanceof ApiError) && failureCount < 2,
  });

  const toggle = useMutation({
    mutationFn: (department: DepartmentRecord) =>
      updateDepartment(department.id, {
        name: department.name,
        isActive: !department.isActive,
        rowVersion: department.rowVersion,
      }),
    onSuccess: async () => {
      setToggling(null);
      setActionError(null);
      await queryClient.invalidateQueries({ queryKey: ['organisation'] });
    },
    onError: (error) => {
      setToggling(null);
      setActionError(messageFor(error));
    },
  });

  const columns = useMemo<ColumnDef<DepartmentRecord, unknown>[]>(() => {
    const base: ColumnDef<DepartmentRecord, unknown>[] = [
      { id: 'name', header: 'Name', accessorFn: (row) => row.name, cell: ({ row }) => row.original.name },
      {
        id: 'status',
        header: 'Status',
        accessorFn: (row) => (row.isActive ? 'Active' : 'Inactive'),
        cell: ({ row }) => (
          <Badge appearance="filled" color={row.original.isActive ? 'brand' : 'subtle'} shape="rounded">
            {row.original.isActive ? 'Active' : 'Inactive'}
          </Badge>
        ),
      },
      {
        id: 'updated',
        header: 'Last changed',
        accessorFn: (row) => row.updatedAtUtc,
        cell: ({ row }) => formatKarachiDate(new Date(row.original.updatedAtUtc)),
      },
    ];

    if (!canManage) {
      return base;
    }

    return [
      ...base,
      {
        id: 'actions',
        header: 'Actions',
        enableSorting: false,
        cell: ({ row }) => (
          <div className={styles.actions}>
            <Button size="small" appearance="subtle" onClick={() => setEditing(row.original)}>
              Rename
            </Button>
            <Button size="small" appearance="subtle" onClick={() => setToggling(row.original)}>
              {row.original.isActive ? 'Deactivate' : 'Reactivate'}
            </Button>
          </div>
        ),
      },
    ];
  }, [canManage]);

  const deactivating = toggling?.isActive ?? false;

  return (
    <section aria-labelledby="departments-title" className={styles.page}>
      <div className={styles.header}>
        <Title1 id="departments-title" as="h1">
          Departments
        </Title1>
        <Badge appearance="outline" color="informative">
          Organisation Management
        </Badge>
      </div>
      <Text>
        Departments group people in the organisation. A department with active people cannot be deactivated. Move or
        deactivate those people first.
      </Text>

      <div className={styles.toolbar}>
        <Switch
          label="Show inactive"
          checked={includeInactive}
          onChange={(_, data) => setIncludeInactive(data.checked)}
        />
        {canManage ? (
          <Button appearance="primary" onClick={() => setEditing('new')}>
            New department
          </Button>
        ) : null}
      </div>

      {actionError ? (
        <Text role="alert" className={styles.error}>
          {actionError}
        </Text>
      ) : null}

      <DataTable
        caption="Departments"
        data={query.data}
        columns={columns}
        isLoading={query.isPending}
        error={query.error}
        onRetry={() => void query.refetch()}
        emptyTitle="No departments yet"
        emptyDescription={canManage ? 'Create the first department.' : 'No departments have been created.'}
      />

      {editing !== null ? (
        <DepartmentDialog
          key={editing === 'new' ? 'new' : editing.id}
          department={editing === 'new' ? null : editing}
          onClose={() => setEditing(null)}
        />
      ) : null}

      <ConfirmDialog
        open={toggling !== null}
        title={deactivating ? 'Deactivate department?' : 'Reactivate department?'}
        description={
          <Text>
            {deactivating
              ? `"${toggling?.name}" will be hidden from new choices. Its history stays in the audit log.`
              : `"${toggling?.name}" will be available for new choices again.`}
          </Text>
        }
        confirmLabel={deactivating ? 'Deactivate' : 'Reactivate'}
        destructive={deactivating}
        busy={toggle.isPending}
        onConfirm={() => {
          if (toggling) {
            toggle.mutate(toggling);
          }
        }}
        onCancel={() => setToggling(null)}
      />
    </section>
  );
}
