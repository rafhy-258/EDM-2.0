import { FluentProvider, webLightTheme } from '@fluentui/react-components';
import { useMemo, useState, type ReactElement } from 'react';
import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom';
import { AppShell, type DevRoleControl } from '../components/AppShell';
import { NoPermissionState, SignInRequiredState } from '../components/StateViews';
import { NAVIGATION, type NavItemDef } from '../navigation/navigation';
import { NotFoundPage } from '../pages/NotFoundPage';
import { PlaceholderPage } from '../pages/PlaceholderPage';
import { type RoleKey } from '../session/roles';
import { devSessionFor, SessionProvider, useSession, type SessionUser } from '../session/session';

/**
 * Root component. In production the session comes from the server. Until the backend exists,
 * production has no session and every protected route shows the sign-in state (deny by default).
 * Development builds offer a role switcher so the menu can be checked for each role.
 */
export function App() {
  const [devRole, setDevRole] = useState<RoleKey | null>(import.meta.env.DEV ? 'REQUESTER' : null);
  const user: SessionUser | null = useMemo(
    () => (import.meta.env.DEV && devRole ? devSessionFor(devRole) : null),
    [devRole],
  );
  const devControl: DevRoleControl | undefined = import.meta.env.DEV
    ? { role: devRole, onChange: setDevRole }
    : undefined;

  return (
    <FluentProvider theme={webLightTheme} style={{ minHeight: '100vh' }}>
      <SessionProvider user={user}>
        <BrowserRouter>
          <AppRoutes devControl={devControl} />
        </BrowserRouter>
      </SessionProvider>
    </FluentProvider>
  );
}

function AppRoutes({ devControl }: { readonly devControl: DevRoleControl | undefined }) {
  const { user } = useSession();
  const childRoutes: ReactElement[] = user
    ? [
        <Route key="index" index element={<Navigate to="/workspace/overview" replace />} />,
        ...NAVIGATION.filter((item) => item.phase === 'P1').map((item) => (
          <Route key={item.id} path={item.route.slice(1)} element={<GuardedPage item={item} />} />
        )),
        <Route key="not-found" path="*" element={<NotFoundPage />} />,
      ]
    : [<Route key="sign-in" path="*" element={<SignInRequiredState />} />];

  return (
    <Routes>
      <Route element={<AppShell devRole={devControl} />}>{childRoutes}</Route>
    </Routes>
  );
}

/** Route guard. The UI check is cosmetic. The server must still check each request (ADR-0007). */
function GuardedPage({ item }: { readonly item: NavItemDef }) {
  const { hasPermission } = useSession();
  if (!hasPermission(item.requiredPermission)) {
    return <NoPermissionState />;
  }
  return <PlaceholderPage item={item} />;
}
