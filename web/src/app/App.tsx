import { FluentProvider, webLightTheme } from '@fluentui/react-components';
import { QueryClient, QueryClientProvider, useQuery, useQueryClient } from '@tanstack/react-query';
import { useMemo, useState } from 'react';
import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom';
import { AuditActivityPage } from '../audit/AuditActivityPage';
import { CategoriesPage } from '../configuration/CategoriesPage';
import { SubcategoriesPage } from '../configuration/SubcategoriesPage';
import { FinancialLimitsPage } from '../configuration/FinancialLimitsPage';
import { ApprovalAuthoritiesPage } from '../configuration/ApprovalAuthoritiesPage';
import { DepartmentsPage } from '../organisation/DepartmentsPage';
import { UsersPage } from '../organisation/UsersPage';
import { ApiError } from '../api/apiError';
import { AppShell, type DevRoleControl } from '../components/AppShell';
import { ErrorState, LoadingState, NoPermissionState } from '../components/StateViews';
import { NAVIGATION, type NavItemDef } from '../navigation/navigation';
import { NotFoundPage } from '../pages/NotFoundPage';
import { PlaceholderPage } from '../pages/PlaceholderPage';
import { SignInPage } from '../pages/SignInPage';
import { type RoleKey } from '../session/roles';
import { devSessionFor, SessionProvider, useSession, type SessionUser } from '../session/session';
import { fetchSession, SESSION_QUERY_KEY, signOut } from '../session/sessionApi';

/** Role fixtures exist only in development builds. Production has no fixture path at all. */
const FIXTURES_AVAILABLE = import.meta.env.DEV;

export interface AppProps {
  /** Starts with a development role fixture instead of the server session. Used by the tests. Ignored outside development. */
  readonly devFixtures?: boolean;
}

interface FixtureState {
  /** null is the development "no session" choice. */
  readonly role: RoleKey | null;
}

/**
 * Root component. The session comes from the server (GET /api/v1/auth/session). A user with no session sees the
 * sign-in page. In development a role fixture can replace the server session, so each role's menu can be checked.
 */
export function App({ devFixtures = false }: AppProps) {
  const [queryClient] = useState(
    () => new QueryClient({ defaultOptions: { queries: { staleTime: 30_000, refetchOnWindowFocus: false } } }),
  );

  return (
    <QueryClientProvider client={queryClient}>
      <FluentProvider theme={webLightTheme} style={{ minHeight: '100vh' }}>
        <BrowserRouter>
          <SessionRoot startWithFixture={devFixtures && FIXTURES_AVAILABLE} />
        </BrowserRouter>
      </FluentProvider>
    </QueryClientProvider>
  );
}

function isUnauthorised(error: unknown): boolean {
  return error instanceof ApiError && error.status === 401;
}

function SessionRoot({ startWithFixture }: { readonly startWithFixture: boolean }) {
  const queryClient = useQueryClient();
  const [fixture, setFixture] = useState<FixtureState | null>(startWithFixture ? { role: 'REQUESTER' } : null);

  const serverSession = useQuery({
    queryKey: SESSION_QUERY_KEY,
    queryFn: ({ signal }) => fetchSession(signal),
    enabled: fixture === null,
    retry: (failureCount, error) => !isUnauthorised(error) && failureCount < 2,
  });

  const fixtureUser = useMemo<SessionUser | null>(
    () => (fixture?.role ? devSessionFor(fixture.role) : null),
    [fixture],
  );

  if (fixture === null && serverSession.isPending) {
    return <LoadingState label="Checking your session" />;
  }
  if (fixture === null && serverSession.isError && !isUnauthorised(serverSession.error)) {
    return <ErrorState onRetry={() => void serverSession.refetch()} />;
  }

  const user: SessionUser | null = fixture !== null ? fixtureUser : (serverSession.data ?? null);

  async function handleSignOut() {
    if (fixture === null) {
      try {
        await signOut();
      } catch {
        // Clear the local session even when the request fails. The cookie expires with its idle timeout.
      }
    }
    queryClient.setQueryData(SESSION_QUERY_KEY, null);
    setFixture(null);
  }

  const devControl: DevRoleControl | undefined =
    FIXTURES_AVAILABLE && fixture !== null ? { role: fixture.role, onChange: (role) => setFixture({ role }) } : undefined;
  const onDevRole = FIXTURES_AVAILABLE ? (role: RoleKey) => setFixture({ role }) : undefined;

  return (
    <SessionProvider user={user}>
      <AppRoutes
        devControl={devControl}
        onDevRole={onDevRole}
        onSignOut={user ? () => void handleSignOut() : undefined}
      />
    </SessionProvider>
  );
}

function AppRoutes({
  devControl,
  onDevRole,
  onSignOut,
}: {
  readonly devControl: DevRoleControl | undefined;
  readonly onDevRole: ((role: RoleKey) => void) | undefined;
  readonly onSignOut: (() => void) | undefined;
}) {
  const { user } = useSession();
  if (!user) {
    return (
      <Routes>
        <Route path="/sign-in" element={<SignInPage onDevRole={onDevRole} />} />
        <Route path="*" element={<Navigate to="/sign-in" replace />} />
      </Routes>
    );
  }

  return (
    <Routes>
      <Route element={<AppShell devRole={devControl} onSignOut={onSignOut} />}>
        <Route path="/sign-in" element={<Navigate to="/workspace/overview" replace />} />
        <Route index element={<Navigate to="/workspace/overview" replace />} />
        {NAVIGATION.filter((item) => item.phase === 'P1').map((item) => (
          <Route key={item.id} path={item.route.slice(1)} element={<GuardedPage item={item} />} />
        ))}
        <Route path="*" element={<NotFoundPage />} />
      </Route>
    </Routes>
  );
}

/** Route guard. The UI check is cosmetic. The server must still check each request (ADR-0007). */
function GuardedPage({ item }: { readonly item: NavItemDef }) {
  const { hasPermission } = useSession();
  const permitted = item.requiredAnyPermission
    ? item.requiredAnyPermission.some((permission) => hasPermission(permission))
    : hasPermission(item.requiredPermission);
  if (!permitted) {
    return <NoPermissionState />;
  }
  if (item.id === 'activity-logs') {
    return <AuditActivityPage />;
  }
  if (item.id === 'categories') {
    return <CategoriesPage />;
  }
  if (item.id === 'subcategories') {
    return <SubcategoriesPage />;
  }
  if (item.id === 'financial-limits') {
    return <FinancialLimitsPage />;
  }
  if (item.id === 'approval-authorities') {
    return <ApprovalAuthoritiesPage />;
  }
  if (item.id === 'departments') {
    return <DepartmentsPage />;
  }
  if (item.id === 'users') {
    return <UsersPage />;
  }
  return <PlaceholderPage item={item} />;
}
