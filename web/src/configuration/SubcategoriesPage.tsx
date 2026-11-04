import { Badge, Button, Field, Select, Switch, Text, Title1 } from '@fluentui/react-components';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import type { ColumnDef } from '@tanstack/react-table';
import { useMemo, useState } from 'react';
import { formatKarachiDate } from '../audit/format';
import { ApiError } from '../api/apiError';
import { ConfirmDialog } from '../components/ConfirmDialog';
import { DataTable } from '../components/DataTable';
import { useSession } from '../session/session';
import {
  deleteSubcategory,
  listCategories,
  listSubcategories,
  type SubcategoryRecord,
} from './configurationApi';
import { messageFor } from './configurationValidation';
import { SubcategoryDialog } from './SubcategoryDialog';
import styles from './ConfigurationPage.module.css';

/**
 * Subcategories (Master Prompt Part 5.1). Each belongs to one category. Names are unique within a category.
 */
export function SubcategoriesPage() {
  const { hasPermission } = useSession();
  const canManage = hasPermission('config.subcategory.manage');
  const queryClient = useQueryClient();
  const [includeInactive, setIncludeInactive] = useState(false);
  const [filterCategoryId, setFilterCategoryId] = useState('');
  const [editing, setEditing] = useState<SubcategoryRecord | 'new' | null>(null);
  const [deleting, setDeleting] = useState<SubcategoryRecord | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);

  const categories = useQuery({
    queryKey: ['configuration', 'categories', true],
    queryFn: () => listCategories(true),
    retry: (failureCount, error) => !(error instanceof ApiError) && failureCount < 2,
  });

  const query = useQuery({
    queryKey: ['configuration', 'subcategories', filterCategoryId, includeInactive],
    queryFn: () => listSubcategories(filterCategoryId || undefined, includeInactive),
    retry: (failureCount, error) => !(error instanceof ApiError) && failureCount < 2,
  });

  const remove = useMutation({
    mutationFn: (subcategory: SubcategoryRecord) => deleteSubcategory(subcategory.id, subcategory.rowVersion),
    onSuccess: async () => {
      setDeleting(null);
      setActionError(null);
      await queryClient.invalidateQueries({ queryKey: ['configuration', 'subcategories'] });
    },
    onError: (error) => {
      setDeleting(null);
      setActionError(messageFor(error));
    },
  });

  const activeCategories = useMemo(
    () => (categories.data ?? []).filter((category) => category.isActive),
    [categories.data],
  );

  const columns = useMemo<ColumnDef<SubcategoryRecord, unknown>[]>(() => {
    const base: ColumnDef<SubcategoryRecord, unknown>[] = [
      { id: 'name', header: 'Name', accessorFn: (row) => row.name, cell: ({ row }) => row.original.name },
      {
        id: 'category',
        header: 'Category',
        accessorFn: (row) => row.categoryName,
        cell: ({ row }) => row.original.categoryName,
      },
      {
        id: 'description',
        header: 'Description',
        accessorFn: (row) => row.description ?? '',
        cell: ({ row }) => row.original.description ?? '',
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
    <section aria-labelledby="subcategories-title" className={styles.page}>
      <div className={styles.header}>
        <Title1 id="subcategories-title" as="h1">
          Subcategories
        </Title1>
        <Badge appearance="outline" color="informative">
          Configuration and Policies
        </Badge>
      </div>
      <Text>
        Subcategories belong to one category. Names are unique within a category. Deactivate a subcategory that is in
        use instead of deleting it.
      </Text>

      <div className={styles.toolbar}>
        <div className={styles.filters}>
          <Field label="Category">
            <Select value={filterCategoryId} onChange={(_, data) => setFilterCategoryId(data.value)}>
              <option value="">All categories</option>
              {(categories.data ?? []).map((category) => (
                <option key={category.id} value={category.id}>
                  {category.isActive ? category.name : `${category.name} (inactive)`}
                </option>
              ))}
            </Select>
          </Field>
          <Switch
            label="Show inactive"
            checked={includeInactive}
            onChange={(_, data) => setIncludeInactive(data.checked)}
          />
        </div>
        {canManage ? (
          <Button appearance="primary" onClick={() => setEditing('new')} disabled={activeCategories.length === 0}>
            New subcategory
          </Button>
        ) : null}
      </div>

      {canManage && !categories.isPending && activeCategories.length === 0 ? (
        <Text>Create an active category before adding subcategories.</Text>
      ) : null}

      {actionError ? (
        <Text role="alert" className={styles.error}>
          {actionError}
        </Text>
      ) : null}

      <DataTable
        caption="Subcategories"
        data={query.data}
        columns={columns}
        isLoading={query.isPending}
        error={query.error}
        onRetry={() => void query.refetch()}
        emptyTitle="No subcategories yet"
        emptyDescription={canManage ? 'Create a subcategory under a category.' : 'No subcategories have been created.'}
      />

      {editing !== null ? (
        <SubcategoryDialog
          key={editing === 'new' ? 'new' : editing.id}
          subcategory={editing === 'new' ? null : editing}
          activeCategories={activeCategories}
          defaultCategoryId={filterCategoryId || null}
          onClose={() => setEditing(null)}
        />
      ) : null}

      <ConfirmDialog
        open={deleting !== null}
        title="Delete subcategory?"
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
