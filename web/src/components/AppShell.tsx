import {
  Avatar,
  Button,
  Hamburger,
  Nav,
  NavDrawer,
  NavDrawerBody,
  NavItem,
  NavSectionHeader,
  Select,
  SearchBox,
  Text,
  Tooltip,
  makeStyles,
  tokens,
} from '@fluentui/react-components';
import { Alert24Regular } from '@fluentui/react-icons';
import { useMemo, useState } from 'react';
import { Outlet, useLocation, useNavigate } from 'react-router-dom';
import { layout } from '../design/tokens';
import { menuFor } from '../navigation/visibleNavigation';
import { useMediaQuery } from '../hooks/useMediaQuery';
import { ROLE_KEYS, ROLE_LABELS, type RoleKey } from '../session/roles';
import { useSession } from '../session/session';

const useStyles = makeStyles({
  root: {
    display: 'flex',
    flexDirection: 'column',
    minHeight: '100vh',
    backgroundColor: tokens.colorNeutralBackground2,
  },
  header: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalM,
    height: `${layout.headerHeightPx}px`,
    padding: `0 ${tokens.spacingHorizontalL}`,
    backgroundColor: tokens.colorNeutralBackground1,
    borderBottom: `1px solid ${tokens.colorNeutralStroke2}`,
    position: 'sticky',
    top: 0,
    zIndex: 2,
  },
  brand: {
    fontWeight: tokens.fontWeightSemibold,
    fontSize: tokens.fontSizeBase400,
    whiteSpace: 'nowrap',
  },
  tenant: {
    color: tokens.colorNeutralForeground3,
    whiteSpace: 'nowrap',
  },
  spacer: { flexGrow: 1 },
  search: { width: 'min(320px, 30vw)' },
  devRole: { display: 'flex', alignItems: 'center', gap: tokens.spacingHorizontalS },
  body: {
    display: 'flex',
    flex: 1,
    minHeight: 0,
  },
  content: {
    flex: 1,
    minWidth: 0,
    padding: tokens.spacingVerticalXL,
    maxWidth: `${layout.contentMaxWidthPx}px`,
    width: '100%',
    boxSizing: 'border-box',
  },
  skipLink: {
    position: 'absolute',
    left: '-9999px',
    ':focus': {
      left: tokens.spacingHorizontalM,
      top: tokens.spacingVerticalS,
      zIndex: 10,
      backgroundColor: tokens.colorNeutralBackground1,
      padding: tokens.spacingVerticalXS,
    },
  },
  drawer: {
    width: `${layout.sidebarWidthPx}px`,
  },
});

export interface DevRoleControl {
  readonly role: RoleKey | null;
  readonly onChange: (role: RoleKey | null) => void;
}

/**
 * Application frame: collapsible grouped navigation (Part 14.2), header with search,
 * notifications, profile and tenant context. The navigation shows only what the session permits.
 * `devRole` is only passed in development builds.
 */
export function AppShell({
  devRole,
  onSignOut,
}: {
  readonly devRole?: DevRoleControl;
  /** Passed only when a session exists. */
  readonly onSignOut?: () => void;
}) {
  const styles = useStyles();
  const navigate = useNavigate();
  const location = useLocation();
  const { user, hasPermission } = useSession();
  const isNarrow = useMediaQuery(`(max-width: ${layout.drawerBreakpointPx}px)`);
  const [open, setOpen] = useState(() => !isNarrow);

  const groups = useMemo(() => menuFor(hasPermission), [hasPermission]);
  const selected = groups
    .flatMap((g) => g.items)
    .filter((item) => location.pathname === item.route || location.pathname.startsWith(`${item.route}/`))
    .sort((a, b) => b.route.length - a.route.length)[0]?.id;

  return (
    <div className={styles.root}>
      <a href="#main-content" className={styles.skipLink}>
        Skip to main content
      </a>
      <header className={styles.header}>
        <Hamburger
          onClick={() => setOpen((o) => !o)}
          aria-label={open ? 'Collapse navigation' : 'Expand navigation'}
          aria-expanded={open}
          aria-controls="main-navigation"
        />
        <Text className={styles.brand}>EDM 2.0</Text>
        <Text className={styles.tenant} size={200}>
          Warmbytes (Pvt) Ltd
        </Text>
        <div className={styles.spacer} />
        <SearchBox className={styles.search} placeholder="Search requests, references, vendors" aria-label="Search" />
        <Tooltip content="Notifications" relationship="label">
          <Button
            appearance="subtle"
            icon={<Alert24Regular />}
            aria-label="Notifications"
            onClick={() => navigate('/workspace/notifications')}
          />
        </Tooltip>
        {devRole ? (
          <div className={styles.devRole}>
            <Select
              aria-label="Development role"
              value={devRole.role ?? 'NONE'}
              onChange={(_, data) => devRole.onChange(data.value === 'NONE' ? null : (data.value as RoleKey))}
              size="small"
            >
              <option value="NONE">No session (sign-in required)</option>
              {ROLE_KEYS.map((key) => (
                <option key={key} value={key}>
                  Dev role: {ROLE_LABELS[key]}
                </option>
              ))}
            </Select>
          </div>
        ) : null}
        {onSignOut ? (
          <Button appearance="subtle" onClick={onSignOut}>
            Sign out
          </Button>
        ) : null}
        <Avatar
          name={user?.displayName ?? 'Guest'}
          size={32}
          aria-label={user ? `Signed in as ${user.displayName}` : 'Not signed in'}
        />
      </header>

      <div className={styles.body}>
        <NavDrawer
          id="main-navigation"
          open={open}
          type={isNarrow ? 'overlay' : 'inline'}
          onOpenChange={(_, data) => setOpen(data.open)}
          selectedValue={selected ?? ''}
          className={styles.drawer}
          aria-label="Main navigation"
        >
          <NavDrawerBody>
            <Nav>
              {groups.map((group) => (
                <div key={group.area}>
                  <NavSectionHeader>{group.area}</NavSectionHeader>
                  {group.items.map((item) => (
                    <NavItem
                      key={item.id}
                      value={item.id}
                      href={item.route}
                      onClick={(event) => {
                        event.preventDefault();
                        navigate(item.route);
                        if (isNarrow) setOpen(false);
                      }}
                    >
                      {item.label}
                    </NavItem>
                  ))}
                </div>
              ))}
            </Nav>
          </NavDrawerBody>
        </NavDrawer>

        <main id="main-content" className={styles.content} tabIndex={-1}>
          <Outlet />
        </main>
      </div>
    </div>
  );
}
