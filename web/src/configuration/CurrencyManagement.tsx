import {
  Badge,
  Button,
  Dialog,
  DialogActions,
  DialogBody,
  DialogContent,
  DialogSurface,
  DialogTitle,
  Field,
  Input,
  Switch,
  Text,
} from '@fluentui/react-components';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import type { ColumnDef } from '@tanstack/react-table';
import { useMemo, useState, type FormEvent } from 'react';
import { formatKarachiDate } from '../audit/format';
import { ApiError } from '../api/apiError';
import { ConfirmDialog } from '../components/ConfirmDialog';
import { DataTable } from '../components/DataTable';
import type { CurrencyRecord } from './configurationApi';
import {
  changeCurrencyRate,
  createCurrency,
  deleteCurrency,
  listCurrencies,
  listCurrencyRates,
  setCurrencyActiveState,
  updateCurrency,
  type CurrencyRateRecord,
} from './configurationApi';
import { messageFor } from './configurationValidation';
import styles from './ConfigurationPage.module.css';

type Editor = { readonly kind: 'create' | 'name' | 'rate'; readonly currency: CurrencyRecord | null };
type Action = { readonly kind: 'activate' | 'deactivate' | 'delete'; readonly currency: CurrencyRecord };

export function CurrencyManagement() {
  const queryClient = useQueryClient();
  const [includeInactive, setIncludeInactive] = useState(true);
  const [editor, setEditor] = useState<Editor | null>(null);
  const [action, setAction] = useState<Action | null>(null);
  const [historyCurrency, setHistoryCurrency] = useState<CurrencyRecord | null>(null);
  const [formError, setFormError] = useState<string | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  const [code, setCode] = useState('');
  const [name, setName] = useState('');
  const [rate, setRate] = useState('');

  const query = useQuery({
    queryKey: ['configuration', 'currencies', includeInactive],
    queryFn: () => listCurrencies(includeInactive),
    retry: (failureCount, error) => !(error instanceof ApiError) && failureCount < 2,
  });
  const historyQuery = useQuery({
    queryKey: ['configuration', 'currency-rates', historyCurrency?.id],
    queryFn: () => {
      if (!historyCurrency) {
        throw new Error('Choose a currency to view its rate history.');
      }
      return listCurrencyRates(historyCurrency.id);
    },
    enabled: historyCurrency !== null,
    retry: (failureCount, error) => !(error instanceof ApiError) && failureCount < 2,
  });

  const refresh = async () => {
    await queryClient.invalidateQueries({ queryKey: ['configuration', 'currencies'] });
  };
  const save = useMutation({
    mutationFn: async () => {
      if (!editor) {
        throw new Error('Choose a currency operation first.');
      }
      if (editor.kind === 'create') {
        return createCurrency({ code: code.trim().toUpperCase(), name: name.trim(), rate: rate.trim() });
      }
      if (!editor.currency) {
        throw new Error('The selected currency is no longer available.');
      }
      return editor.kind === 'name'
        ? updateCurrency(editor.currency.id, name.trim(), editor.currency.rowVersion)
        : changeCurrencyRate(editor.currency.id, rate.trim(), editor.currency.rowVersion);
    },
    onSuccess: async () => {
      setEditor(null);
      setFormError(null);
      await refresh();
    },
    onError: (error) => setFormError(messageFor(error)),
  });
  const runAction = useMutation({
    mutationFn: async () => {
      if (!action) {
        throw new Error('Choose a currency operation first.');
      }
      const { currency } = action;
      if (action.kind === 'delete') {
        await deleteCurrency(currency.id, currency.rowVersion);
        return;
      }
      await setCurrencyActiveState(currency.id, action.kind === 'activate', currency.rowVersion);
    },
    onSuccess: async () => {
      setAction(null);
      setActionError(null);
      await refresh();
    },
    onError: (error) => {
      setAction(null);
      setActionError(messageFor(error));
    },
  });

  const columns = useMemo<ColumnDef<CurrencyRecord, unknown>[]>(() => [
    { id: 'code', header: 'Code', accessorFn: (row) => row.code, cell: ({ row }) => row.original.code },
    { id: 'name', header: 'Name', accessorFn: (row) => row.name, cell: ({ row }) => row.original.name },
    {
      id: 'rate',
      header: 'Rate (PKR)',
      accessorFn: (row) => row.rate ?? '',
      cell: ({ row }) => (row.original.rate === null ? 'Not set' : row.original.rate),
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
      id: 'actions',
      header: 'Actions',
      enableSorting: false,
      cell: ({ row }) => {
        const currency = row.original;
        return (
          <div className={styles.actions}>
            <Button size="small" appearance="subtle" onClick={() => setHistoryCurrency(currency)}>Rates</Button>
            <Button size="small" appearance="subtle" onClick={() => openEditor('name', currency)}>Edit name</Button>
            {!currency.isBase ? (
              <Button size="small" appearance="subtle" onClick={() => openEditor('rate', currency)}>Change rate</Button>
            ) : null}
            {!currency.isBase ? (
              <>
                <Button
                  size="small"
                  appearance="subtle"
                  onClick={() => setAction({ kind: currency.isActive ? 'deactivate' : 'activate', currency })}
                >
                  {currency.isActive ? 'Deactivate' : 'Activate'}
                </Button>
                <Button size="small" appearance="subtle" onClick={() => setAction({ kind: 'delete', currency })}>
                  Delete
                </Button>
              </>
            ) : null}
          </div>
        );
      },
    },
  ], []);

  function openEditor(kind: Editor['kind'], currency: CurrencyRecord | null = null) {
    setFormError(null);
    setCode(currency?.code ?? '');
    setName(currency?.name ?? '');
    setRate(kind === 'rate' ? (currency?.rate ?? '') : '');
    setEditor({ kind, currency });
  }

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setFormError(null);
    if (editor?.kind === 'create' && !/^[a-zA-Z]{3}$/.test(code.trim())) {
      setFormError('Enter a three-letter currency code, for example USD.');
      return;
    }
    if ((editor?.kind === 'create' || editor?.kind === 'name') && !name.trim()) {
      setFormError('Enter a currency name.');
      return;
    }
    if ((editor?.kind === 'create' || editor?.kind === 'rate') && !/^\d+(?:\.\d{1,4})?$/.test(rate.trim())) {
      setFormError('Enter a positive rate with at most four decimal places.');
      return;
    }
    save.mutate();
  }

  const historyColumns = useMemo<ColumnDef<CurrencyRateRecord, unknown>[]>(() => [
    { id: 'rate', header: 'Rate (PKR)', accessorFn: (row) => row.rate, cell: ({ row }) => row.original.rate },
    {
      id: 'from',
      header: 'Effective from',
      accessorFn: (row) => row.effectiveFrom,
      cell: ({ row }) => formatKarachiDate(new Date(row.original.effectiveFrom)),
    },
    {
      id: 'to',
      header: 'Effective to',
      accessorFn: (row) => row.effectiveTo ?? '',
      cell: ({ row }) => (row.original.effectiveTo ? formatKarachiDate(new Date(row.original.effectiveTo)) : 'Current'),
    },
  ], []);

  return (
    <section aria-labelledby="currencies-title" className={styles.page}>
      <div className={styles.header}>
        <Text as="h2" id="currencies-title" size={500} weight="semibold">Currencies and Rates</Text>
      </div>
      <Text>Manage currency codes and their PKR conversion rates. Rate changes take effect immediately and retain history.</Text>
      <Text className={styles.muted}>Changes apply as soon as they are saved. Maker-checker approval is part of M1A-06 and is not available yet.</Text>
      <div className={styles.toolbar}>
        <Switch label="Show inactive" checked={includeInactive} onChange={(_, data) => setIncludeInactive(data.checked)} />
        <Button appearance="primary" onClick={() => openEditor('create')}>New currency</Button>
      </div>
      {actionError ? <Text role="alert" className={styles.error}>{actionError}</Text> : null}
      <DataTable
        caption="Currencies and current rates"
        data={query.data}
        columns={columns}
        isLoading={query.isPending}
        error={query.error}
        onRetry={() => void query.refetch()}
        emptyTitle="No currencies yet"
        emptyDescription="Create the first currency."
      />

      {editor ? (
        <Dialog open modalType="modal" onOpenChange={(_, data) => !data.open && setEditor(null)}>
          <DialogSurface>
            <form onSubmit={submit} noValidate>
              <DialogBody>
                <DialogTitle>
                  {editor.kind === 'create' ? 'New currency' : editor.kind === 'name' ? `Edit ${editor.currency?.code}` : `Change ${editor.currency?.code} rate`}
                </DialogTitle>
                <DialogContent className={styles.form}>
                  {editor.kind === 'create' ? (
                    <Field label="Currency code" required hint="Three letters; the code cannot be changed after creation.">
                      <Input value={code} maxLength={3} autoComplete="off" onChange={(_, data) => setCode(data.value)} />
                    </Field>
                  ) : null}
                  {editor.kind !== 'rate' ? (
                    <Field label="Currency name" required>
                      <Input value={name} maxLength={100} autoComplete="off" onChange={(_, data) => setName(data.value)} />
                    </Field>
                  ) : null}
                  {editor.kind !== 'name' ? (
                    <Field label="Rate (PKR per currency unit)" required hint="Use a positive value with up to four decimal places.">
                      <Input value={rate} inputMode="decimal" autoComplete="off" onChange={(_, data) => setRate(data.value)} />
                    </Field>
                  ) : null}
                  {formError ? <Text role="alert" className={styles.error}>{formError}</Text> : null}
                </DialogContent>
                <DialogActions>
                  <Button appearance="secondary" disabled={save.isPending} onClick={() => setEditor(null)}>Cancel</Button>
                  <Button appearance="primary" type="submit" disabled={save.isPending}>Save</Button>
                </DialogActions>
              </DialogBody>
            </form>
          </DialogSurface>
        </Dialog>
      ) : null}

      <ConfirmDialog
        open={action !== null}
        title={action?.kind === 'delete' ? 'Delete currency?' : `${action?.kind === 'deactivate' ? 'Deactivate' : 'Activate'} currency?`}
        description={
          <Text>
            {action?.kind === 'delete'
              ? `${action.currency.code} can be deleted only when it has no rate history or other usage.`
              : action?.kind === 'deactivate'
                ? `${action.currency.code} will no longer be available in active currency pickers. Its rate history stays available.`
                : `${action?.currency.code} will become available in active currency pickers.`}
          </Text>
        }
        confirmLabel={action?.kind === 'delete' ? 'Delete' : action?.kind === 'deactivate' ? 'Deactivate' : 'Activate'}
        destructive={action?.kind !== 'activate'}
        busy={runAction.isPending}
        onConfirm={() => runAction.mutate()}
        onCancel={() => setAction(null)}
      />

      {historyCurrency ? (
        <Dialog open modalType="modal" onOpenChange={(_, data) => !data.open && setHistoryCurrency(null)}>
          <DialogSurface>
            <DialogBody>
              <DialogTitle>{historyCurrency.code} rate history</DialogTitle>
              <DialogContent>
                <DataTable
                  caption={`${historyCurrency.code} rate history`}
                  data={historyQuery.data}
                  columns={historyColumns}
                  isLoading={historyQuery.isPending}
                  error={historyQuery.error}
                  onRetry={() => void historyQuery.refetch()}
                  emptyTitle="No rate history"
                  emptyDescription="No rates have been recorded."
                />
              </DialogContent>
              <DialogActions>
                <Button appearance="secondary" onClick={() => setHistoryCurrency(null)}>Close</Button>
              </DialogActions>
            </DialogBody>
          </DialogSurface>
        </Dialog>
      ) : null}
    </section>
  );
}
