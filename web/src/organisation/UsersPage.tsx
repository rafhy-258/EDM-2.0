import { Badge, Button, Dropdown, Field, Option, Switch, Text, Title1 } from '@fluentui/react-components';
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
import { DirectoryUserDialog } from './DirectoryUserDialog';
import {
  listDepartmentOptions,
  listDirectoryUsers,
  updateDirectoryUser,
  type DirectoryUserRecord,
} from './organisationApi';

const ALL_DEPARTMENTS = '';

/**
 * The organisation directory (data model section 6). People are deactivated, not deleted. Each person belongs to one
 * active department. Every change is audited by the server.
 */
export function UsersPage() {
  const { hasPermission } = useSession();
  const canManage = hasPermission('org.user.manage');
  const queryClient = useQueryClient();
  const [departmentFilter, setDepartmentFilter] = useState(ALL_DEPARTMENTS);
  const [includeInactive, setIncludeInactive] = useState(false);
  const [editing, setEditing] = useState<DirectoryUserRecord | 'new' | null>(null);
  const [toggling, setToggling] = useState<DirectoryUserRecord | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);

  const departments = useQuery({
    queryKey: ['organisation', 'department-options'],
    queryFn: listDepartmentOptions,
    retry: (failureCount, error) => !(error instanceof ApiError) && failureCount < 2,
  });

  const departmentNames = useMemo(
    () => new Map((departments.data ?? []).map((d) => [d.id, d.name])),
    [departments.data],
  );

  const query = useQuery({
    queryKey: ['organisation', 'users', departmentFilter, includeInactive],
    queryFn: () => listDirectoryUsers(departmentFilter === ALL_DEPARTMENTS ? undefined : departmentFilter, includeInactive),
    retry: (failureCount, error) => !(error instanceof ApiError) && failureCount < 2,
  });

  const toggle = useMutation({
    mutationFn: (person: DirectoryUserRecord) =>
      updateDirectoryUser(person.id, {
        employeeId: person.employeeId,
        name: person.name,
        email: person.email,
        departmentId: person.departmentId,
        isActive: !person.isActive,
        rowVersion: person.rowVersion,
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

  const columns = useMemo<ColumnDef<DirectoryUserRecord, unknown>[]>(() => {
    const base: ColumnDef<DirectoryUserRecord, unknown>[] = [
      {
        id: 'employeeId',
        header: 'Employee ID',
        accessorFn: (row) => row.employeeId,
        cell: ({ row }) => row.original.employeeId,
      },
      { id: 'name', header: 'Name', accessorFn: (row) => row.name, cell: ({ row }) => row.original.name },
      {
        id: 'email',
        header: 'Email',
        accessorFn: (row) => row.email ?? '',
        cell: ({ row }) => row.original.email ?? '',
      },
      {
        id: 'department',
        header: 'Department',
        accessorFn: (row) => departmentNames.get(row.departmentId) ?? '',
        cell: ({ row }) => departmentNames.get(row.original.departmentId) ?? 'Unknown department',
      },
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
              Edit
            </Button>
            <Button size="small" appearance="subtle" onClick={() => setToggling(row.original)}>
              {row.original.isActive ? 'Deactivate' : 'Reactivate'}
            </Button>
          </div>
        ),
      },
    ];
  }, [canManage, departmentNames]);

  const deactivating = toggling?.isActive ?? false;
  const filterLabel = departmentFilter === ALL_DEPARTMENTS
    ? 'All departments'
    : (departmentNames.get(departmentFilter) ?? '');

  return (
    <section aria-labelledby="users-title" className={styles.page}>
      <div className={styles.header}>
        <Title1 id="users-title" as="h1">
          Users
        </Title1>
        <Badge appearance="outline" color="informative">
          Identity and Access
        </Badge>
      </div>
      <Text>
        The organisation directory. Each person belongs to one active department. A person who leaves is deactivated,
        not deleted.
      </Text>

      <div className={styles.toolbar}>
        <Field label="Department">
          <Dropdown
            aria-label="Filter by department"
            value={filterLabel}
            selectedOptions={[departmentFilter]}
            onOptionSelect={(_, data) => setDepartmentFilter(data.optionValue ?? ALL_DEPARTMENTS)}
          >
            <Option value={ALL_DEPARTMENTS} text="All departments">
              All departments
            </Option>
            {(departments.data ?? []).map((d) => (
              <Option key={d.id} value={d.id} text={d.name}>
                {d.name}
              </Option>
            ))}
          </Dropdown>
        </Field>
        <Switch
          label="Show inactive"
          checked={includeInactive}
          onChange={(_, data) => setIncludeInactive(data.checked)}
        />
        {canManage ? (
          <Button appearance="primary" onClick={() => setEditing('new')} disabled={!departments.data?.length}>
            Add person
          </Button>
        ) : null}
      </div>

      {actionError ? (
        <Text role="alert" className={styles.error}>
          {actionError}
        </Text>
      ) : null}

      <DataTable
        caption="Users"
        data={query.data}
        columns={columns}
        isLoading={query.isPending}
        error={query.error}
        onRetry={() => void query.refetch()}
        emptyTitle="No people in this view"
        emptyDescription={canManage ? 'Add a person to the directory.' : 'No people match this filter.'}
      />

      {editing !== null && departments.data ? (
        <DirectoryUserDialog
          key={editing === 'new' ? 'new' : editing.id}
          person={editing === 'new' ? null : editing}
          departments={departments.data}
          onClose={() => setEditing(null)}
        />
      ) : null}

      <ConfirmDialog
        open={toggling !== null}
        title={deactivating ? 'Deactivate person?' : 'Reactivate person?'}
        description={
          <Text>
            {deactivating
              ? `"${toggling?.name}" will be hidden from new choices. Their history stays in the audit log.`
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
