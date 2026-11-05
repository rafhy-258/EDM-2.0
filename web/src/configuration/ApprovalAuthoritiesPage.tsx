import {
  Badge,
  Button,
  Checkbox,
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
  Title1,
} from '@fluentui/react-components';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import type { ColumnDef } from '@tanstack/react-table';
import { useCallback, useMemo, useState, type FormEvent } from 'react';
import { formatKarachiDate } from '../audit/format';
import { ApiError } from '../api/apiError';
import { ConfirmDialog } from '../components/ConfirmDialog';
import { DataTable } from '../components/DataTable';
import { EmptyState, ErrorState, LoadingState } from '../components/StateViews';
import { useSession } from '../session/session';
import { messageFor } from './configurationValidation';
import {
  createEcMember,
  createRecommendingAuthority,
  deleteEcMember,
  deleteRecommendingAuthority,
  listActionTagSets,
  listApprovalDepartments,
  listApprovalDirectoryUsers,
  listEcMembers,
  listRecommendingAuthorities,
  updateActionTagSet,
  updateEcMember,
  updateRecommendingAuthority,
  type ActionTagSetRecord,
  type EcMemberRecord,
  type RecommendingAuthorityRecord,
} from './configurationApi';
import styles from './ApprovalAuthoritiesPage.module.css';

type Tab = 'members' | 'authorities' | 'actions';
type Editor = { readonly kind: 'member'; readonly record: null } | {
  readonly kind: 'authority';
  readonly record: RecommendingAuthorityRecord | null;
};
type PendingAction =
  | { readonly kind: 'activate' | 'deactivate' | 'delete'; readonly target: 'member'; readonly record: EcMemberRecord }
  | { readonly kind: 'activate' | 'deactivate' | 'delete'; readonly target: 'authority'; readonly record: RecommendingAuthorityRecord };

const DESIGNATIONS = [
  ['EC_MEMBER', 'EC Member'],
  ['CEO', 'CEO'],
  ['CFO', 'CFO'],
  ['HEAD_ADMIN_GST', 'Head of Admin & GST'],
] as const;
const ACTIONS = [
  ['APPROVE', 'Approve'],
  ['REJECT', 'Reject'],
  ['REVERT', 'Revert'],
  ['FORWARD', 'Forward'],
] as const;

export function ApprovalAuthoritiesPage() {
  const { hasPermission } = useSession();
  const canManageMembers = hasPermission('config.ec.pool.manage');
  const canManageAuthorities = hasPermission('config.ra.manage');
  const canManageActions = hasPermission('config.actiontags.manage');
  const queryClient = useQueryClient();
  const [tab, setTab] = useState<Tab>(() =>
    canManageMembers ? 'members' : canManageAuthorities ? 'authorities' : 'actions');
  const [includeInactive, setIncludeInactive] = useState(false);
  const [memberSearch, setMemberSearch] = useState('');
  const [authoritySearch, setAuthoritySearch] = useState('');
  const [designation, setDesignation] = useState('');
  const [editor, setEditor] = useState<Editor | null>(null);
  const [pendingAction, setPendingAction] = useState<PendingAction | null>(null);
  const [formError, setFormError] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<Readonly<Record<string, string>>>({});
  const [actionError, setActionError] = useState<string | null>(null);
  const [selectedDepartment, setSelectedDepartment] = useState('');
  const [selectedUser, setSelectedUser] = useState('');
  const [selectedDesignation, setSelectedDesignation] = useState<string>('EC_MEMBER');
  const [title, setTitle] = useState('');

  const membersQuery = useQuery({
    queryKey: ['approval-authorities', 'members', includeInactive, designation, memberSearch],
    queryFn: () => listEcMembers(designation || undefined, includeInactive, memberSearch),
    enabled: canManageMembers && tab === 'members',
    retry: (count, error) => !(error instanceof ApiError) && count < 2,
  });
  const authoritiesQuery = useQuery({
    queryKey: ['approval-authorities', 'authorities', includeInactive, authoritySearch],
    queryFn: () => listRecommendingAuthorities(includeInactive, authoritySearch),
    enabled: canManageAuthorities && tab === 'authorities',
    retry: (count, error) => !(error instanceof ApiError) && count < 2,
  });
  const tagSetsQuery = useQuery({
    queryKey: ['approval-authorities', 'action-tags'],
    queryFn: listActionTagSets,
    enabled: canManageActions && tab === 'actions',
    retry: (count, error) => !(error instanceof ApiError) && count < 2,
  });
  const departmentsQuery = useQuery({
    queryKey: ['approval-authorities', 'departments'],
    queryFn: listApprovalDepartments,
    enabled: editor?.kind === 'authority',
    retry: (count, error) => !(error instanceof ApiError) && count < 2,
  });
  const candidateRoute = editor?.kind === 'member' ? 'ec-members' : 'recommending-authorities';
  const candidatesQuery = useQuery({
    queryKey: ['approval-authorities', 'candidates', candidateRoute, editor?.kind === 'authority' ? selectedDepartment : undefined],
    queryFn: () => listApprovalDirectoryUsers(
      candidateRoute,
      editor?.kind === 'authority' ? selectedDepartment || undefined : undefined,
    ),
    enabled: editor !== null,
    retry: (count, error) => !(error instanceof ApiError) && count < 2,
  });
  const openAuthorityEditor = useCallback((record: RecommendingAuthorityRecord | null = null) => {
    setFormError(null);
    setFieldErrors({});
    setSelectedDepartment(record?.departmentId ?? '');
    setSelectedUser(record?.userId ?? '');
    setTitle(record?.title ?? '');
    setEditor({ kind: 'authority', record });
  }, []);

  const refresh = async () => {
    await queryClient.invalidateQueries({ queryKey: ['approval-authorities'] });
  };
  const save = useMutation({
    mutationFn: async () => {
      if (!editor) throw new Error('Choose an approval-authority operation first.');
      if (editor.kind === 'member') {
        return createEcMember({ userId: selectedUser, designation: selectedDesignation });
      }
      if (!editor.record) {
        return createRecommendingAuthority({
          title: title.trim(),
          departmentId: selectedDepartment,
          userId: selectedUser,
        });
      }
      return updateRecommendingAuthority(editor.record.id, {
        departmentId: selectedDepartment,
        userId: selectedUser,
        isActive: editor.record.isActive,
        rowVersion: editor.record.rowVersion,
      });
    },
    onSuccess: async () => {
      setEditor(null);
      setFormError(null);
      await refresh();
    },
    onError: (error) => {
      const errors = error instanceof ApiError
        ? Object.fromEntries(Object.entries(error.fieldErrors)
          .flatMap(([field, messages]) => messages[0] ? [[field, messages[0]]] : []))
        : {};
      setFieldErrors(errors);
      setFormError(Object.keys(errors).length === 0 ? messageFor(error) : null);
    },
  });
  const runAction = useMutation({
    mutationFn: async () => {
      if (!pendingAction) throw new Error('Choose an approval-authority action first.');
      if (pendingAction.target === 'member') {
        const member = pendingAction.record;
        if (pendingAction.kind === 'delete') return deleteEcMember(member.id, member.rowVersion);
        return updateEcMember(member.id, pendingAction.kind === 'activate', member.rowVersion);
      }
      const authority = pendingAction.record;
      if (pendingAction.kind === 'delete') {
        return deleteRecommendingAuthority(authority.id, authority.rowVersion);
      }
      return updateRecommendingAuthority(authority.id, {
        departmentId: authority.departmentId,
        userId: authority.userId,
        isActive: pendingAction.kind === 'activate',
        rowVersion: authority.rowVersion,
      });
    },
    onSuccess: async () => {
      setPendingAction(null);
      setActionError(null);
      await refresh();
    },
    onError: (error) => {
      setPendingAction(null);
      setActionError(messageFor(error));
    },
  });
  const saveTagSet = useMutation({
    mutationFn: ({ set, actions }: { readonly set: ActionTagSetRecord; readonly actions: readonly string[] }) =>
      updateActionTagSet(set.id, actions, set.rowVersion),
    onSuccess: async () => {
      setActionError(null);
      await refresh();
    },
    onError: (error) => setActionError(messageFor(error)),
  });

  const memberColumns = useMemo<ColumnDef<EcMemberRecord, unknown>[]>(() => [
    {
      id: 'member',
      header: 'Member',
      accessorFn: (row) => row.userName ?? row.userId,
      cell: ({ row }) => (
        <div>
          <Text weight="semibold">{row.original.userName ?? 'Directory user unavailable'}</Text>
          <br />
          <Text size={200} className={styles.muted}>{row.original.employeeId ?? row.original.userId}</Text>
        </div>
      ),
    },
    { id: 'designation', header: 'Designation', accessorFn: (row) => row.designationLabel, cell: ({ row }) => row.original.designationLabel },
    { id: 'department', header: 'Department', accessorFn: (row) => row.departmentName ?? '', cell: ({ row }) => row.original.departmentName ?? '—' },
    {
      id: 'status',
      header: 'Status',
      accessorFn: (row) => row.isActive ? 'Active' : 'Inactive',
      cell: ({ row }) => <Badge appearance="filled" color={row.original.isActive ? 'brand' : 'subtle'}>{row.original.isActive ? 'Active' : 'Inactive'}</Badge>,
    },
    {
      id: 'updated',
      header: 'Last changed',
      accessorFn: (row) => row.updatedAtUtc,
      cell: ({ row }) => formatKarachiDate(new Date(row.original.updatedAtUtc)),
    },
    ...(canManageMembers ? [{
      id: 'actions',
      header: 'Actions',
      enableSorting: false,
      cell: ({ row }: { row: { original: EcMemberRecord } }) => (
        <div className={styles.actions}>
          <Button size="small" appearance="subtle" onClick={() => setPendingAction({
            kind: row.original.isActive ? 'deactivate' : 'activate',
            target: 'member',
            record: row.original,
          })}>{row.original.isActive ? 'Deactivate' : 'Reactivate'}</Button>
          <Button size="small" appearance="subtle" onClick={() => setPendingAction({
            kind: 'delete', target: 'member', record: row.original,
          })}>Delete</Button>
        </div>
      ),
    }] : []),
  ], [canManageMembers]);

  const authorityColumns = useMemo<ColumnDef<RecommendingAuthorityRecord, unknown>[]>(() => [
    { id: 'title', header: 'Authority', accessorFn: (row) => row.title, cell: ({ row }) => <Text weight="semibold">{row.original.title}</Text> },
    {
      id: 'assignedTo',
      header: 'Assigned to',
      accessorFn: (row) => row.userName ?? '',
      cell: ({ row }) => (
        <div>
          <Text>{row.original.userName ?? 'Directory user unavailable'}</Text>
          <br />
          <Text size={200} className={styles.muted}>{row.original.employeeId ?? row.original.userId}</Text>
        </div>
      ),
    },
    { id: 'department', header: 'Department', accessorFn: (row) => row.departmentName ?? '', cell: ({ row }) => row.original.departmentName ?? '—' },
    { id: 'actionsTag', header: 'Default actions', accessorFn: (row) => row.actionTagSetName, cell: ({ row }) => row.original.actionTagSetName },
    {
      id: 'status',
      header: 'Status',
      accessorFn: (row) => row.isActive ? 'Active' : 'Inactive',
      cell: ({ row }) => <Badge appearance="filled" color={row.original.isActive ? 'brand' : 'subtle'}>{row.original.isActive ? 'Active' : 'Inactive'}</Badge>,
    },
    ...(canManageAuthorities ? [{
      id: 'actions',
      header: 'Actions',
      enableSorting: false,
      cell: ({ row }: { row: { original: RecommendingAuthorityRecord } }) => (
        <div className={styles.actions}>
          <Button size="small" appearance="subtle" onClick={() => openAuthorityEditor(row.original)}>Edit assignment</Button>
          <Button size="small" appearance="subtle" onClick={() => setPendingAction({
            kind: row.original.isActive ? 'deactivate' : 'activate',
            target: 'authority',
            record: row.original,
          })}>{row.original.isActive ? 'Deactivate' : 'Reactivate'}</Button>
          <Button size="small" appearance="subtle" onClick={() => setPendingAction({
            kind: 'delete', target: 'authority', record: row.original,
          })}>Delete</Button>
        </div>
      ),
    }] : []),
  ], [canManageAuthorities, openAuthorityEditor]);

  function openMemberEditor() {
    setFormError(null);
    setFieldErrors({});
    setSelectedUser('');
    setSelectedDesignation('EC_MEMBER');
    setEditor({ kind: 'member', record: null });
  }

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setFormError(null);
    setFieldErrors({});
    if (!selectedUser) {
      setFieldErrors({ userId: 'Choose an active directory user.' });
      return;
    }
    if (editor?.kind === 'authority') {
      if (!selectedDepartment) {
        setFieldErrors({ departmentId: 'Choose an active department.' });
        return;
      }
      if (!editor.record && (!title.trim() || title.trim().length > 200 || !/^[A-Za-z0-9 ]+$/.test(title.trim()))) {
        setFieldErrors({ title: 'Enter a title using letters, numbers and spaces, up to 200 characters.' });
        return;
      }
    }
    save.mutate();
  }

  function clearFieldError(field: string) {
    setFieldErrors((current) => {
      if (!(field in current)) return current;
      return Object.fromEntries(Object.entries(current).filter(([key]) => key !== field));
    });
  }

  const activeCount = (membersQuery.data ?? []).filter((item) => item.isActive).length;
  const activeAuthorities = (authoritiesQuery.data ?? []).filter((item) => item.isActive).length;

  return (
    <section aria-labelledby="approval-authorities-title" className={styles.page}>
      <header className={styles.hero}>
        <div className={styles.heroText}>
          <Text className={styles.eyebrow}>Configuration and Policies · Phase 1A</Text>
          <Title1 id="approval-authorities-title" as="h1">Approval Authorities</Title1>
          <Text className={styles.muted}>
            Maintain who can recommend and approve, and the default actions available to each designation.
          </Text>
        </div>
        <div className={styles.summary}>
          {canManageMembers ? <div className={styles.summaryItem}><Text size={200}>Active matches</Text><Text size={400} weight="semibold">{activeCount}</Text></div> : null}
          {canManageAuthorities ? <div className={styles.summaryItem}><Text size={200}>Active matches</Text><Text size={400} weight="semibold">{activeAuthorities}</Text></div> : null}
        </div>
      </header>

      <nav className={styles.tabs} aria-label="Approval authority sections">
        {canManageMembers ? <button className={styles.tab} type="button" aria-pressed={tab === 'members'} onClick={() => setTab('members')}>EC member pool</button> : null}
        {canManageAuthorities ? <button className={styles.tab} type="button" aria-pressed={tab === 'authorities'} onClick={() => setTab('authorities')}>Recommending authorities</button> : null}
        {canManageActions ? <button className={styles.tab} type="button" aria-pressed={tab === 'actions'} onClick={() => setTab('actions')}>Action defaults</button> : null}
      </nav>

      <div className={styles.notice} role="note">
        <Text>
          <strong>Changes apply immediately.</strong> Each change is audited. Policy-version publishing and maker-checker approval
          are not yet available and are scheduled for M1A-06.
        </Text>
      </div>

      {actionError ? <Text role="alert" className={styles.error}>{actionError}</Text> : null}

      {tab === 'members' && canManageMembers ? (
        <>
          <div className={styles.toolbar}>
            <div className={styles.filters}>
              <Input className={styles.search} type="search" aria-label="Search EC members" placeholder="Search name or employee ID" value={memberSearch} onChange={(_, data) => setMemberSearch(data.value)} />
              <Field label="Designation">
                <select aria-label="Filter by designation" value={designation} onChange={(event) => setDesignation(event.currentTarget.value)}>
                  <option value="">All designations</option>
                  {DESIGNATIONS.map(([code, label]) => <option key={code} value={code}>{label}</option>)}
                </select>
              </Field>
              <Switch label="Show inactive" checked={includeInactive} onChange={(_, data) => setIncludeInactive(data.checked)} />
            </div>
            <Button appearance="primary" onClick={openMemberEditor}>Add EC member</Button>
          </div>
          <DataTable
            caption="EC member pool"
            data={membersQuery.data}
            columns={memberColumns}
            isLoading={membersQuery.isPending}
            error={membersQuery.error}
            onRetry={() => void membersQuery.refetch()}
            emptyTitle="No EC members found"
            emptyDescription="Add active directory users to make them available for future EC approval levels."
          />
        </>
      ) : null}

      {tab === 'authorities' && canManageAuthorities ? (
        <>
          <div className={styles.toolbar}>
            <div className={styles.filters}>
              <Input className={styles.search} type="search" aria-label="Search recommending authorities" placeholder="Search title, person or department" value={authoritySearch} onChange={(_, data) => setAuthoritySearch(data.value)} />
              <Switch label="Show inactive" checked={includeInactive} onChange={(_, data) => setIncludeInactive(data.checked)} />
            </div>
            <Button appearance="primary" onClick={() => openAuthorityEditor()}>Add authority</Button>
          </div>
          <DataTable
            caption="Recommending authorities"
            data={authoritiesQuery.data}
            columns={authorityColumns}
            isLoading={authoritiesQuery.isPending}
            error={authoritiesQuery.error}
            onRetry={() => void authoritiesQuery.refetch()}
            emptyTitle="No recommending authorities found"
            emptyDescription="Add an authority and assign it to a user in the matching department."
          />
        </>
      ) : null}

      {tab === 'actions' && canManageActions ? (
        <ActionTagSets
          sets={tagSetsQuery.data}
          loading={tagSetsQuery.isPending}
          error={tagSetsQuery.error}
          onRetry={() => void tagSetsQuery.refetch()}
          onSave={(set, actions) => saveTagSet.mutate({ set, actions })}
          saving={saveTagSet.isPending}
        />
      ) : null}

      <Dialog open={editor !== null} onOpenChange={(_, data) => !data.open && !save.isPending && setEditor(null)}>
        <DialogSurface>
          <form onSubmit={submit}>
            <DialogBody>
              <DialogTitle>
                {editor?.kind === 'member' ? 'Add EC pool member' : editor?.record ? 'Edit authority assignment' : 'Add recommending authority'}
              </DialogTitle>
              <DialogContent>
                <div className={styles.form}>
                  {editor?.kind === 'member' ? (
                    <>
                      <Field label="Designation" required validationMessage={fieldErrors['designation']}>
                        <select value={selectedDesignation} onChange={(event) => {
                          setSelectedDesignation(event.currentTarget.value);
                          clearFieldError('designation');
                        }}>
                          {DESIGNATIONS.map(([code, label]) => <option key={code} value={code}>{label}</option>)}
                        </select>
                      </Field>
                      <Field label="Active directory user" required validationMessage={fieldErrors['userId']}>
                        <select value={selectedUser} onChange={(event) => {
                          setSelectedUser(event.currentTarget.value);
                          clearFieldError('userId');
                        }} disabled={candidatesQuery.isPending}>
                          <option value="">Select a user</option>
                          {(candidatesQuery.data ?? []).map((user) => (
                            <option key={user.id} value={user.id}>{user.name} · {user.employeeId} · {user.departmentName}</option>
                          ))}
                        </select>
                      </Field>
                      {candidatesQuery.data?.length === 0
                        ? <Text size={200} className={styles.muted}>No active directory users are available.</Text>
                        : null}
                      <Text size={200} className={styles.muted}>A person can hold multiple designations. An inactive duplicate must be reactivated instead of added again.</Text>
                    </>
                  ) : editor?.kind === 'authority' ? (
                    <>
                      {!editor.record ? (
                        <Field label="Authority title" required validationMessage={fieldErrors['title']}>
                          <Input value={title} maxLength={200} onChange={(_, data) => {
                            setTitle(data.value);
                            clearFieldError('title');
                          }} placeholder="e.g. Finance Review" />
                        </Field>
                      ) : (
                        <Field label="Authority title">
                          <Input value={title} readOnly aria-describedby="authority-title-note" />
                          <Text id="authority-title-note" size={200} className={styles.muted}>Titles are immutable after creation.</Text>
                        </Field>
                      )}
                      <Field label="Department" required validationMessage={fieldErrors['departmentId']}>
                        <select
                          value={selectedDepartment}
                          onChange={(event) => {
                            setSelectedDepartment(event.currentTarget.value);
                            setSelectedUser('');
                            clearFieldError('departmentId');
                            clearFieldError('userId');
                          }}
                          disabled={departmentsQuery.isPending}
                        >
                          <option value="">Select a department</option>
                          {(departmentsQuery.data ?? []).map((department) => (
                            <option key={department.id} value={department.id}>{department.name}</option>
                          ))}
                        </select>
                      </Field>
                      <Field label="Assigned user" required validationMessage={fieldErrors['userId']}>
                        <select value={selectedUser} onChange={(event) => {
                          setSelectedUser(event.currentTarget.value);
                          clearFieldError('userId');
                        }} disabled={!selectedDepartment || candidatesQuery.isPending}>
                          <option value="">Select a user</option>
                          {(candidatesQuery.data ?? []).map((user) => (
                            <option key={user.id} value={user.id}>{user.name} · {user.employeeId}</option>
                          ))}
                        </select>
                      </Field>
                      {selectedDepartment && candidatesQuery.data?.length === 0
                        ? <Text size={200} className={styles.muted}>No active users are available in this department.</Text>
                        : null}
                      <Text size={200} className={styles.muted}>Only active users in the selected department are available. The first authority decision completes its stage.</Text>
                    </>
                  ) : null}
                  {formError ? <Text role="alert" className={styles.error}>{formError}</Text> : null}
                  {editor?.kind === 'authority' && departmentsQuery.isError
                    ? <Text role="alert" className={styles.error}>{messageFor(departmentsQuery.error)}</Text>
                    : null}
                  {editor !== null && candidatesQuery.isError
                    ? <Text role="alert" className={styles.error}>{messageFor(candidatesQuery.error)}</Text>
                    : null}
                </div>
              </DialogContent>
              <DialogActions>
                <Button appearance="secondary" type="button" disabled={save.isPending} onClick={() => setEditor(null)}>Cancel</Button>
                <Button appearance="primary" type="submit" disabled={save.isPending || candidatesQuery.isPending || (editor?.kind === 'authority' && departmentsQuery.isPending)}>
                  {save.isPending ? 'Saving…' : editor?.record ? 'Save assignment' : 'Add'}
                </Button>
              </DialogActions>
            </DialogBody>
          </form>
        </DialogSurface>
      </Dialog>

      <ConfirmDialog
        open={pendingAction !== null}
        title={pendingAction?.kind === 'delete' ? 'Delete this record?' : `${pendingAction?.kind === 'deactivate' ? 'Deactivate' : 'Reactivate'} this record?`}
        description={pendingAction?.kind === 'delete'
          ? 'The record will be removed from active configuration. If it is referenced by a future approval rule, deletion will be blocked.'
          : 'This change affects which people can be selected in future approval configuration.'}
        confirmLabel={pendingAction?.kind === 'delete' ? 'Delete' : pendingAction?.kind === 'deactivate' ? 'Deactivate' : 'Reactivate'}
        destructive={pendingAction?.kind !== 'activate'}
        busy={runAction.isPending}
        onConfirm={() => runAction.mutate()}
        onCancel={() => !runAction.isPending && setPendingAction(null)}
      />
    </section>
  );
}

function ActionTagSets({
  sets,
  loading,
  error,
  onRetry,
  onSave,
  saving,
}: {
  readonly sets: readonly ActionTagSetRecord[] | undefined;
  readonly loading: boolean;
  readonly error: unknown;
  readonly onRetry: () => void;
  readonly onSave: (set: ActionTagSetRecord, actions: readonly string[]) => void;
  readonly saving: boolean;
}) {
  if (loading) return <LoadingState label="Loading action defaults" />;
  if (error) {
    const description = error instanceof ApiError ? error.message : 'The action defaults could not be loaded.';
    return <ErrorState title="Unable to load action defaults" description={description} onRetry={onRetry} />;
  }
  if (!sets?.length) return <EmptyState title="No action defaults configured" description="Refresh the page or contact an administrator." />;

  return (
    <div className={styles.cards}>
      {sets.map((set) => (
        <ActionTagSetEditor key={`${set.id}:${set.rowVersion}`} set={set} saving={saving} onSave={onSave} />
      ))}
    </div>
  );
}

function ActionTagSetEditor({
  set,
  saving,
  onSave,
}: {
  readonly set: ActionTagSetRecord;
  readonly saving: boolean;
  readonly onSave: (set: ActionTagSetRecord, actions: readonly string[]) => void;
}) {
  const [actions, setActions] = useState<readonly string[]>(set.allowedActions);
  const changed = actions.length !== set.allowedActions.length ||
    actions.some((action) => !set.allowedActions.includes(action));

  function toggle(action: string, checked: boolean) {
    if (action === 'APPROVE') return;
    setActions((current) => checked
      ? [...current, action]
      : current.filter((value) => value !== action));
  }

  return (
    <section className={styles.card} aria-label={`${set.designationLabel} default actions`}>
      <div className={styles.cardHeader}>
        <div>
          <Text weight="semibold">{set.designationLabel}</Text>
          <br />
          <Text size={200} className={styles.muted}>{set.name}</Text>
        </div>
        <Badge appearance="outline" color="informative">Default</Badge>
      </div>
      <div className={styles.checkboxes}>
        {ACTIONS.map(([code, label]) => (
          <Checkbox
            key={code}
            label={label}
            checked={actions.includes(code)}
            disabled={code === 'APPROVE' || (code === 'FORWARD' && set.designation !== 'DEPARTMENT_HEAD')}
            onChange={(_, data) => toggle(code, data.checked === true)}
          />
        ))}
      </div>
      {set.designation === 'EC_MEMBER'
        ? <Text size={200} className={styles.muted}>Forward is never available to EC members.</Text>
        : null}
      <Button
        appearance="primary"
        disabled={!changed || saving}
        onClick={() => onSave(set, actions)}
      >
        {saving ? 'Saving…' : 'Save actions'}
      </Button>
    </section>
  );
}
