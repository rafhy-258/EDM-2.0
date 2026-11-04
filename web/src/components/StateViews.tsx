import { Button, Spinner, Text, Title3 } from '@fluentui/react-components';
import type { ReactNode } from 'react';
import styles from './StateViews.module.css';

/**
 * Standard screen states (Master Prompt 14.2: every screen has loading, empty, error and
 * no-permission states). Messages follow the error catalogue in Part 21.3.
 */

export function LoadingState({ label = 'Loading' }: { readonly label?: string }) {
  return (
    <div className={styles.container} role="status" aria-live="polite">
      <Spinner size="medium" label={label} />
    </div>
  );
}

export function EmptyState({
  title,
  description,
  action,
}: {
  readonly title: string;
  readonly description?: string;
  readonly action?: ReactNode;
}) {
  return (
    <section className={styles.container} aria-labelledby="empty-state-title">
      <Title3 id="empty-state-title" as="h2">
        {title}
      </Title3>
      {description ? <Text>{description}</Text> : null}
      {action}
    </section>
  );
}

export function ErrorState({
  title = 'Something went wrong',
  description = 'The information could not be loaded. Please try again.',
  onRetry,
}: {
  readonly title?: string;
  readonly description?: string;
  readonly onRetry?: () => void;
}) {
  return (
    <section className={styles.container} role="alert">
      <Title3 as="h2">{title}</Title3>
      <Text>{description}</Text>
      {onRetry ? (
        <Button appearance="primary" onClick={onRetry}>
          Try again
        </Button>
      ) : null}
    </section>
  );
}

/** EDM-AUTH-002: Not permitted. */
export function NoPermissionState() {
  return (
    <section className={styles.container} aria-labelledby="no-permission-title">
      <Title3 id="no-permission-title" as="h2">
        You do not have access to this
      </Title3>
      <Text>If you need access, ask your system administrator.</Text>
      <Text size={200}>Code: EDM-AUTH-002</Text>
    </section>
  );
}

/** EDM-AUTH-001: Not authenticated. */
export function SignInRequiredState({ onSignIn }: { readonly onSignIn?: () => void }) {
  return (
    <section className={styles.container} aria-labelledby="sign-in-title">
      <Title3 id="sign-in-title" as="h2">
        Please sign in
      </Title3>
      <Text>Sign in with your bank account to continue.</Text>
      {onSignIn ? (
        <Button appearance="primary" onClick={onSignIn}>
          Sign in
        </Button>
      ) : null}
      <Text size={200}>Code: EDM-AUTH-001</Text>
    </section>
  );
}
