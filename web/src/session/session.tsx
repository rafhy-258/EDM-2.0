import { createContext, useContext, useMemo, type ReactNode } from 'react';
import type { Permission } from './permissions';
import { ROLE_PERMISSIONS, type RoleKey } from './roles';

export interface SessionUser {
  readonly displayName: string;
  readonly department: string;
  readonly permissions: ReadonlySet<Permission>;
}

interface SessionContextValue {
  readonly user: SessionUser | null;
  hasPermission(permission: Permission | null): boolean;
}

const SessionContext = createContext<SessionContextValue | null>(null);

/**
 * Builds a development session for a role fixture.
 * Production builds never call this. Until the backend session exists, production has no user
 * and every protected screen shows the sign-in state (deny by default).
 */
export function devSessionFor(role: RoleKey): SessionUser {
  return {
    displayName: `Dev user (${role.toLowerCase().replace(/_/g, ' ')})`,
    department: 'Development',
    permissions: new Set(ROLE_PERMISSIONS[role]),
  };
}

export function SessionProvider({
  user,
  children,
}: {
  readonly user: SessionUser | null;
  readonly children: ReactNode;
}) {
  const value = useMemo<SessionContextValue>(
    () => ({
      user,
      hasPermission(permission) {
        // Null means "any signed-in user". Without a session nothing is allowed.
        if (!user) return false;
        if (permission === null) return true;
        return user.permissions.has(permission);
      },
    }),
    [user],
  );
  return <SessionContext.Provider value={value}>{children}</SessionContext.Provider>;
}

export function useSession(): SessionContextValue {
  const value = useContext(SessionContext);
  if (!value) {
    throw new Error('useSession must be used inside SessionProvider');
  }
  return value;
}
