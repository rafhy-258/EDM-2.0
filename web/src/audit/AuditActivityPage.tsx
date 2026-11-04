import {
  Button,
  Field,
  Input,
  Select,
  Text,
  Title1,
} from '@fluentui/react-components';
import { keepPreviousData, useQuery, type UseQueryResult } from '@tanstack/react-query';
import { useState, type FormEvent } from 'react';
import { EmptyState, ErrorState, LoadingState, NoPermissionState, SignInRequiredState } from '../components/StateViews';
import {
  ApiError,
  AUDIT_PAGE_SIZE,
  type AuditEventPage,
  EMPTY_AUDIT_FILTERS,
  buildAuditQuery,
  fetchAuditEvents,
  pageCountFor,
  validateAuditFilters,
  type AuditEvent,
  type AuditFilters,
} from './auditApi';
import { AuditEventDialog } from './AuditEventDialog';
import { AuditEventTable } from './AuditEventTable';
import styles from './AuditActivityPage.module.css';

/**
 * Activity Logs (Part 10.4): the audit viewer. Read-only. Filters and paging run on the server.
 * The route is guarded by audit.view in the UI, and the server checks it again on every request.
 */
export function AuditActivityPage() {
  const [draft, setDraft] = useState<AuditFilters>(EMPTY_AUDIT_FILTERS);
  const [applied, setApplied] = useState<AuditFilters>(EMPTY_AUDIT_FILTERS);
  const [page, setPage] = useState(1);
  const [selected, setSelected] = useState<AuditEvent | null>(null);
  const [formError, setFormError] = useState<string | null>(null);

  const query = buildAuditQuery(applied, page, AUDIT_PAGE_SIZE);
  const result = useQuery({
    queryKey: ['audit-events', query],
    queryFn: ({ signal }) => fetchAuditEvents(query, signal),
    placeholderData: keepPreviousData,
    retry: (failureCount, error) => !(error instanceof ApiError) && failureCount < 2,
  });

  const data = result.data;
  const pageCount = data ? pageCountFor(data.totalCount, data.pageSize) : 1;

  function applyFilters(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const problem = validateAuditFilters(draft);
    setFormError(problem);
    if (problem) return;
    setApplied(draft);
    setPage(1);
  }

  function clearFilters() {
    setDraft(EMPTY_AUDIT_FILTERS);
    setApplied(EMPTY_AUDIT_FILTERS);
    setFormError(null);
    setPage(1);
  }

  return (
    <section aria-labelledby="page-title" className={styles.page}>
      <Title1 id="page-title" as="h1">
        Activity Logs
      </Title1>
      <Text>
        Every change in EDM is recorded here. Times are shown in Asia/Karachi. Events are read-only and cannot be edited or deleted.
      </Text>

      <form aria-label="Filter audit events" onSubmit={applyFilters} className={styles.filters}>
        <Field label="From date">
          <Input type="date" value={draft.fromDate} onChange={(_, d) => setDraft({ ...draft, fromDate: d.value })} />
        </Field>
        <Field label="To date">
          <Input type="date" value={draft.toDate} onChange={(_, d) => setDraft({ ...draft, toDate: d.value })} />
        </Field>
        <Field label="Module">
          <Input value={draft.module} onChange={(_, d) => setDraft({ ...draft, module: d.value })} />
        </Field>
        <Field label="Action">
          <Input value={draft.action} onChange={(_, d) => setDraft({ ...draft, action: d.value })} />
        </Field>
        <Field label="Entity type">
          <Input value={draft.entityType} onChange={(_, d) => setDraft({ ...draft, entityType: d.value })} />
        </Field>
        <Field label="Entity id">
          <Input value={draft.entityId} onChange={(_, d) => setDraft({ ...draft, entityId: d.value })} />
        </Field>
        <Field label="Outcome">
          <Select
            value={draft.outcome}
            onChange={(_, d) => setDraft({ ...draft, outcome: d.value as AuditFilters['outcome'] })}
          >
            <option value="">Any</option>
            <option value="Success">Success</option>
            <option value="Failure">Failure</option>
          </Select>
        </Field>
        <div className={styles.actions}>
          <Button type="submit" appearance="primary">
            Apply filters
          </Button>
          <Button type="button" appearance="secondary" onClick={clearFilters}>
            Clear
          </Button>
        </div>
        {formError ? (
          <Text role="alert" className={styles.formError}>
            {formError}
          </Text>
        ) : null}
      </form>

      <ResultArea
        result={result}
        page={page}
        pageCount={pageCount}
        onPrevious={() => setPage((p) => Math.max(1, p - 1))}
        onNext={() => setPage((p) => Math.min(pageCount, p + 1))}
        onShowDetails={setSelected}
      />

      <AuditEventDialog event={selected} onClose={() => setSelected(null)} />
    </section>
  );
}

function ResultArea({
  result,
  page,
  pageCount,
  onPrevious,
  onNext,
  onShowDetails,
}: {
  readonly result: UseQueryResult<AuditEventPage, Error>;
  readonly page: number;
  readonly pageCount: number;
  readonly onPrevious: () => void;
  readonly onNext: () => void;
  readonly onShowDetails: (event: AuditEvent) => void;
}) {
  if (result.isPending) {
    return <LoadingState label="Loading audit events" />;
  }

  if (result.error) {
    const error = result.error;
    if (error instanceof ApiError && error.status === 401) return <SignInRequiredState />;
    if (error instanceof ApiError && error.status === 403) return <NoPermissionState />;
    return (
      <div>
        <ErrorState
          description={error instanceof ApiError ? error.message : undefined}
          onRetry={() => void result.refetch()}
        />
        {error instanceof ApiError && Object.keys(error.fieldErrors).length > 0 ? (
          <ul>
            {Object.entries(error.fieldErrors).flatMap(([field, messages]) =>
              messages.map((message) => <li key={`${field}-${message}`}>{message}</li>),
            )}
          </ul>
        ) : null}
      </div>
    );
  }

  const data = result.data;
  if (data.items.length === 0) {
    return <EmptyState title="No audit events match these filters" description="Change the filters and apply them again." />;
  }

  return (
    <div className={styles.results}>
      <div className={styles.summary} aria-live="polite">
        <Text>
          {data.totalCount} event{data.totalCount === 1 ? '' : 's'}
          {result.isFetching ? ' (updating)' : ''}
        </Text>
      </div>
      <AuditEventTable caption="Audit events" events={data.items} onShowDetails={onShowDetails} />
      <nav aria-label="Audit events pagination" className={styles.pager}>
        <Button appearance="secondary" disabled={page <= 1} onClick={onPrevious}>
          Previous
        </Button>
        <span>
          Page {page} of {pageCount}
        </span>
        <Button appearance="secondary" disabled={page >= pageCount} onClick={onNext}>
          Next
        </Button>
      </nav>
    </div>
  );
}
