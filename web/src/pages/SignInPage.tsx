import { Button, Field, Input, makeStyles, Select, Text, Title2, tokens } from '@fluentui/react-components';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useState, type FormEvent } from 'react';
import { useNavigate } from 'react-router-dom';
import { ApiError } from '../api/apiError';
import { ROLE_KEYS, ROLE_LABELS, type RoleKey } from '../session/roles';
import { SESSION_QUERY_KEY, signIn } from '../session/sessionApi';

const useStyles = makeStyles({
  page: {
    maxWidth: '420px',
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalM,
  },
  form: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalM,
  },
  development: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalS,
    marginTop: tokens.spacingVerticalXL,
    paddingTop: tokens.spacingVerticalM,
    borderTop: `1px solid ${tokens.colorNeutralStroke2}`,
  },
});

/** The same text for a wrong password, an unknown user, a disabled account and a locked account (no account enumeration). */
const WRONG_CREDENTIALS = 'The username or password is not correct.';

function messageFor(error: unknown): string {
  if (error instanceof ApiError) {
    if (error.status === 401) return WRONG_CREDENTIALS;
    if (error.status === 422) return 'Enter your username and password.';
  }
  return 'Sign-in is not available right now. Please try again.';
}

/**
 * Local sign-in (ADR-0006). The password is sent once, as JSON, over the same origin, and never stored.
 * `onDevRole` is passed only in development builds. It offers a role fixture so the menu can be checked without an account.
 */
export function SignInPage({ onDevRole }: { readonly onDevRole?: (role: RoleKey) => void }) {
  const styles = useStyles();
  const queryClient = useQueryClient();
  const navigate = useNavigate();
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [devRole, setDevRole] = useState<RoleKey>('REQUESTER');
  const [clientError, setClientError] = useState<string | null>(null);

  const mutation = useMutation({
    mutationFn: () => signIn(username.trim(), password),
    onSuccess: (user) => {
      queryClient.setQueryData(SESSION_QUERY_KEY, user);
      setPassword('');
      navigate('/workspace/overview', { replace: true });
    },
  });

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!username.trim() || password.length === 0) {
      setClientError('Enter your username and password.');
      return;
    }
    setClientError(null);
    mutation.mutate();
  }

  const message = clientError ?? (mutation.isError ? messageFor(mutation.error) : null);

  return (
    <main className={styles.page}>
      <Title2 as="h1">Please sign in</Title2>
      <Text>Sign in with your EDM account.</Text>
      <form className={styles.form} onSubmit={submit} aria-label="Sign in">
        <Field label="Username">
          <Input
            name="username"
            autoComplete="username"
            value={username}
            onChange={(_, data) => setUsername(data.value)}
          />
        </Field>
        <Field label="Password">
          <Input
            name="password"
            type="password"
            autoComplete="current-password"
            value={password}
            onChange={(_, data) => setPassword(data.value)}
          />
        </Field>
        <Button type="submit" appearance="primary" disabled={mutation.isPending}>
          {mutation.isPending ? 'Signing in…' : 'Sign in'}
        </Button>
      </form>
      {message ? <Text role="alert">{message}</Text> : null}

      {onDevRole ? (
        <section className={styles.development} aria-labelledby="development-sign-in-title">
          <Text id="development-sign-in-title" weight="semibold">
            Development only
          </Text>
          <Text size={200}>Continue as a role fixture, with no account. This section is not in production builds.</Text>
          <Select
            aria-label="Fixture role"
            value={devRole}
            onChange={(_, data) => setDevRole(data.value as RoleKey)}
          >
            {ROLE_KEYS.map((key) => (
              <option key={key} value={key}>
                {ROLE_LABELS[key]}
              </option>
            ))}
          </Select>
          <Button appearance="secondary" onClick={() => onDevRole(devRole)}>
            Continue as this role
          </Button>
        </section>
      ) : null}
    </main>
  );
}
