import { Button, Field, Input, Select, Text } from '@fluentui/react-components';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useState, type FormEvent } from 'react';
import { useNavigate } from 'react-router-dom';
import { ApiError } from '../api/apiError';
import { ROLE_KEYS, ROLE_LABELS, type RoleKey } from '../session/roles';
import { SESSION_QUERY_KEY, signIn } from '../session/sessionApi';
import styles from './SignInPage.module.css';

/** The same text for a wrong password, an unknown user, a disabled account and a locked account. */
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
 * The role fixture is available only in development builds.
 */
export function SignInPage({ onDevRole }: { readonly onDevRole?: (role: RoleKey) => void }) {
  const queryClient = useQueryClient();
  const navigate = useNavigate();
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [showPassword, setShowPassword] = useState(false);
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
      <section className={styles.brandPanel} aria-label="EDM 2.0">
        <div className={styles.brandContent}>
          <a className={styles.brand} href="/sign-in" aria-label="EDM 2.0 home">
            <span className={styles.brandMark} aria-hidden="true">
              <svg viewBox="0 0 40 40" fill="none">
                <path d="M20 3.5 34 9v9.2c0 8.5-5.8 15-14 18.3C11.8 33.2 6 26.7 6 18.2V9l14-5.5Z" />
                <path d="m13.3 20.2 4.4 4.3 9.2-9.2" />
              </svg>
            </span>
            <span className={styles.brandName}>EDM<span>2.0</span></span>
          </a>

          <div className={styles.hero}>
            <Text className={styles.eyebrow}>EXPENSE GOVERNANCE, REFINED</Text>
            <h1 className={styles.heroTitle}>Clear decisions.<br />Confident control.</h1>
            <p className={styles.heroCopy}>
              A secure workspace for managing delegated approvals, policy and financial governance across your organisation.
            </p>
          </div>

          <div className={styles.trustBlock}>
            <div className={styles.trustIcon} aria-hidden="true">
              <svg viewBox="0 0 24 24" fill="none">
                <path d="M12 3 19 6v5.1c0 4.3-2.9 8.1-7 9.9-4.1-1.8-7-5.6-7-9.9V6l7-3Z" />
                <path d="m9 12 2 2 4-4" />
              </svg>
            </div>
            <div>
              <strong>Governance you can trust</strong>
              <span>Every action is protected and accountable.</span>
            </div>
          </div>
        </div>
        <div className={styles.decorativeOrb} aria-hidden="true" />
        <div className={styles.gridPattern} aria-hidden="true" />
        <p className={styles.brandFooter}>WARMBytes <span>·</span> Thoughtful technology for better decisions</p>
      </section>

      <section className={styles.formPanel} aria-labelledby="sign-in-title">
        <div className={styles.formWrap}>
          <div className={styles.mobileBrand} aria-hidden="true">
            <span className={styles.mobileBrandMark}>
              <svg viewBox="0 0 40 40" fill="none">
                <path d="M20 3.5 34 9v9.2c0 8.5-5.8 15-14 18.3C11.8 33.2 6 26.7 6 18.2V9l14-5.5Z" />
                <path d="m13.3 20.2 4.4 4.3 9.2-9.2" />
              </svg>
            </span>
            <span>EDM<span>2.0</span></span>
          </div>
          <div className={styles.formHeading}>
            <Text className={styles.formEyebrow}>WELCOME BACK</Text>
            <h2 id="sign-in-title">Sign in to your account</h2>
            <p>Enter your credentials to access your secure workspace.</p>
          </div>

          <form className={styles.form} onSubmit={submit} aria-label="Sign in" noValidate>
            <Field label="Username" required>
              <Input
                name="username"
                autoComplete="username"
                autoCapitalize="none"
                autoCorrect="off"
                placeholder="Enter your username"
                value={username}
                onChange={(_, data) => {
                  setUsername(data.value);
                  setClientError(null);
                }}
              />
            </Field>
            <Field label="Password" required>
              <div className={styles.passwordField}>
                <Input
                  name="password"
                  aria-label="Password"
                  type={showPassword ? 'text' : 'password'}
                  autoComplete="current-password"
                  placeholder="Enter your password"
                  value={password}
                  onChange={(_, data) => {
                    setPassword(data.value);
                    setClientError(null);
                  }}
                />
                <button
                  type="button"
                  className={styles.passwordToggle}
                  aria-label={showPassword ? 'Hide password' : 'Show password'}
                  aria-pressed={showPassword}
                  onClick={() => setShowPassword((visible) => !visible)}
                >
                  {showPassword ? (
                    <svg viewBox="0 0 24 24" aria-hidden="true">
                      <path d="M3 3 21 21M10.6 10.7a2 2 0 0 0 2.7 2.7M9.9 5.2A10.7 10.7 0 0 1 12 5c5.2 0 8.5 4.4 9.5 6.2a1.6 1.6 0 0 1 0 1.6 14 14 0 0 1-3.1 3.7M6.2 6.2a15 15 0 0 0-3.7 5 1.6 1.6 0 0 0 0 1.6C3.5 14.6 6.8 19 12 19c.9 0 1.8-.2 2.6-.4" />
                    </svg>
                  ) : (
                    <svg viewBox="0 0 24 24" aria-hidden="true">
                      <path d="M2.5 12s3.2-6 9.5-6 9.5 6 9.5 6-3.2 6-9.5 6-9.5-6-9.5-6Z" />
                      <circle cx="12" cy="12" r="2.5" />
                    </svg>
                  )}
                </button>
              </div>
            </Field>
            {message ? (
              <div className={styles.error} role="alert">
                <span aria-hidden="true">!</span>
                {message}
              </div>
            ) : null}
            <Button
              type="submit"
              appearance="primary"
              className={styles.submitButton}
              disabled={mutation.isPending}
            >
              {mutation.isPending ? 'Signing in…' : 'Sign in'}
              {!mutation.isPending ? <span aria-hidden="true">→</span> : null}
            </Button>
          </form>

          <div className={styles.secureNote}>
            <svg viewBox="0 0 20 20" fill="none" aria-hidden="true">
              <rect x="4" y="8.5" width="12" height="9" rx="2" />
              <path d="M6.5 8.5V6a3.5 3.5 0 0 1 7 0v2.5M10 12v2" />
            </svg>
            <span>Your sign-in is encrypted and securely managed.</span>
          </div>

          {onDevRole ? (
            <section className={styles.development} aria-labelledby="development-sign-in-title">
              <Text id="development-sign-in-title" weight="semibold">Development access</Text>
              <Text size={200}>Development-only role fixture. This option is not available in production.</Text>
              <Select
                aria-label="Fixture role"
                value={devRole}
                onChange={(_, data) => setDevRole(data.value as RoleKey)}
              >
                {ROLE_KEYS.map((key) => (
                  <option key={key} value={key}>{ROLE_LABELS[key]}</option>
                ))}
              </Select>
              <Button appearance="secondary" onClick={() => onDevRole(devRole)}>
                Continue as this role
              </Button>
            </section>
          ) : null}
        </div>
        <footer className={styles.formFooter}>
          <span>© {new Date().getFullYear()} Warmbytes (Pvt) Ltd</span>
          <span className={styles.footerDivider} aria-hidden="true">·</span>
          <span>EDM 2.0</span>
        </footer>
      </section>
    </main>
  );
}
