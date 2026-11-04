import { Badge, Button, Switch, Text, Title1 } from '@fluentui/react-components';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import type { ColumnDef } from '@tanstack/react-table';
import { useMemo, useState } from 'react';
import { formatKarachiDate } from '../audit/format';
import { ConfirmDialog } from '../components/ConfirmDialog';
import { DataTable } from '../components/DataTable';
import { ApiError } from '../api/apiError';
import { useSession } from '../session/session';
import { CategoryDialog } from './CategoryDialog';
import { deleteCategory, listCategories, type CategoryRecord } from './configurationApi';
import { messageFor } from './configurationValidation';
import styles from './ConfigurationPage.module.css';

/**
 * Categories (Master Prompt Part 5.1). Deactivate a category that is still in use. A category with subcategories,
 * or one that is used, cannot be deleted. Every change is audited by the server.
 */
export function CategoriesPage() {
  const { hasPermission } = useSession();
  const canManage = hasPermission('config.category.manage');
  const queryClient = useQueryClient();
  const [includeInactive, setIncludeInactive] = useState(false);
  const [editing, setEditing] = useState<CategoryRecord | 'new' | null>(null);
  const [deleting, setDeleting] = useState<CategoryRecord | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);

  const query = useQuery({
    queryKey: ['configuration', 'categories', includeInactive],
    queryFn: () => listCategories(includeInactive),
    retry: (failureCount, error) => !(error instanceof ApiError) && failureCount < 2,
  });

  const remove = useMutation({
    mutationFn: (category: CategoryRecord) => deleteCategory(category.id, category.rowVersion),
    onSuccess: async () => {
      setDeleting(null);
      setActionError(null);
      await queryClient.invalidateQueries({ queryKey: ['configuration', 'categories'] });
    },
    onError: (error) => {
      setDeleting(null);
      setActionError(messageFor(error));
    },
  });

  const columns = useMemo<ColumnDef<CategoryRecord, unknown>[]>(() => {
    const base: ColumnDef<CategoryRecord, unknown>[] = [
      { id: 'name', header: 'Name', accessorFn: (row) => row.name, cell: ({ row }) => row.original.name },
      {
        id: 'description',
        header: 'Description',
        accessorFn: (row) => row.description ?? '',
        cell: ({ row }) => row.original.description ?? '',
      },
      {
        id: 'subcategories',
        header: 'Subcategories',
        accessorFn: (row) => row.subcategoryCount,
        cell: ({ row }) => row.original.subcategoryCount,
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
            <Button size="small" appearance="subtle" onClick={() => setDeleting(row.original)}>
              Delete
            </Button>
          </div>
        ),
      },
    ];
  }, [canManage]);

  return (
    <section aria-labelledby="categories-title" className={styles.page}>
      <div className={styles.header}>
        <Title1 id="categories-title" as="h1">
          Categories
        </Title1>
        <Badge appearance="outline" color="informative">
          Configuration and Policies
        </Badge>
      </div>
      <Text>
        Categories group expense requests. A category with subcategories, or one that is in use, cannot be deleted.
        Deactivate it instead.
      </Text>

      <div className={styles.toolbar}>
        <Switch
          label="Show inactive"
          checked={includeInactive}
          onChange={(_, data) => setIncludeInactive(data.checked)}
        />
        {canManage ? (
          <Button appearance="primary" onClick={() => setEditing('new')}>
            New category
          </Button>
        ) : null}
      </div>

      {actionError ? (
        <Text role="alert" className={styles.error}>
          {actionError}
        </Text>
      ) : null}

      <DataTable
        caption="Categories"
        data={query.data}
        columns={columns}
        isLoading={query.isPending}
        error={query.error}
        onRetry={() => void query.refetch()}
        emptyTitle="No categories yet"
        emptyDescription={canManage ? 'Create the first category.' : 'No categories have been created.'}
      />

      {editing !== null ? (
        <CategoryDialog
          key={editing === 'new' ? 'new' : editing.id}
          category={editing === 'new' ? null : editing}
          onClose={() => setEditing(null)}
        />
      ) : null}

      <ConfirmDialog
        open={deleting !== null}
        title="Delete category?"
        description={
          <Text>
            &quot;{deleting?.name}&quot; will be removed from the lists. Its history stays in the audit log.
          </Text>
        }
        confirmLabel="Delete"
        destructive
        busy={remove.isPending}
        onConfirm={() => {
          if (deleting) {
            remove.mutate(deleting);
          }
        }}
        onCancel={() => setDeleting(null)}
      />
    </section>
  );
}
