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
import {
  Alert24Regular,
  AppsList20Regular,
  Branch20Regular,
  Building20Regular,
  ChartMultiple20Regular,
  ClipboardTask20Regular,
  Document20Regular,
  History20Regular,
  Home20Regular,
  Key20Regular,
  LockClosed20Regular,
  Money20Regular,
  Organization20Regular,
  People20Regular,
  Receipt20Regular,
  ShieldCheckmark20Regular,
  Settings20Regular,
  Tag20Regular,
} from '@fluentui/react-icons';
import { useMemo, useState, type ReactElement } from 'react';
import { Outlet, useLocation, useNavigate } from 'react-router-dom';
import { layout } from '../design/tokens';
import { menuFor } from '../navigation/visibleNavigation';
import type { NavArea } from '../navigation/navigation';
import { useMediaQuery } from '../hooks/useMediaQuery';
import { ROLE_KEYS, ROLE_LABELS, type RoleKey } from '../session/roles';
import { useSession } from '../session/session';

const areaIcons: Record<NavArea, ReactElement> = {
  Workspace: <Home20Regular />,
  'Expense Management': <Receipt20Regular />,
  'Workflow and Approvals': <ClipboardTask20Regular />,
  'Organisation Management': <Building20Regular />,
  'Configuration and Policies': <Settings20Regular />,
  'Identity and Access': <Key20Regular />,
  'Reports and Analytics': <ChartMultiple20Regular />,
  'Audit and Compliance': <ShieldCheckmark20Regular />,
  Integrations: <AppsList20Regular />,
  'System Administration': <Settings20Regular />,
};

const itemIcons: Record<string, ReactElement> = {
  overview: <Home20Regular />,
  'my-tasks': <ClipboardTask20Regular />,
  'my-requests': <Document20Regular />,
  'workspace-notifications': <Alert24Regular />,
  'create-expense': <Receipt20Regular />,
  opex: <Money20Regular />,
  capex: <Building20Regular />,
  'submitted-requests': <Document20Regular />,
  'expense-register': <AppsList20Regular />,
  'approval-inbox': <ClipboardTask20Regular />,
  'approval-policies': <ShieldCheckmark20Regular />,
  departments: <Building20Regular />,
  branches: <Branch20Regular />,
  hierarchy: <Organization20Regular />,
  categories: <Tag20Regular />,
  subcategories: <Tag20Regular />,
  'financial-limits': <Money20Regular />,
  'approval-authorities': <People20Regular />,
  'opex-capex-policies': <Document20Regular />,
  users: <People20Regular />,
  roles: <ShieldCheckmark20Regular />,
  permissions: <Key20Regular />,
  sso: <LockClosed20Regular />,
  mfa: <LockClosed20Regular />,
  'expense-reports': <ChartMultiple20Regular />,
  'department-spending': <Money20Regular />,
  'audit-reports': <ShieldCheckmark20Regular />,
  exports: <AppsList20Regular />,
  'activity-logs': <History20Regular />,
  'approval-history': <History20Regular />,
  'policy-changes': <History20Regular />,
};

const useStyles = makeStyles({
  root: {
    display: 'flex',
    flexDirection: 'column',
    height: '100dvh',
    overflow: 'hidden',
    backgroundColor: tokens.colorNeutralBackground2,
  },
  header: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalM,
    height: `${layout.headerHeightPx}px`,
    flexShrink: 0,
    padding: `0 ${tokens.spacingHorizontalL}`,
    backgroundColor: tokens.colorNeutralBackground1,
    borderBottom: `1px solid ${tokens.colorNeutralStroke2}`,
    position: 'sticky',
    top: 0,
    zIndex: 2,
    boxShadow: '0 1px 3px rgb(16 36 59 / 4%)',
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
    overflow: 'hidden',
  },
  content: {
    flex: 1,
    minWidth: 0,
    minHeight: 0,
    overflowY: 'auto',
    padding: tokens.spacingVerticalXL,
    maxWidth: `${layout.contentMaxWidthPx}px`,
    width: '100%',
    boxSizing: 'border-box',
  },
  drawer: {
    display: 'flex',
    flexDirection: 'column',
    flexShrink: 0,
    width: `${layout.sidebarWidthPx}px`,
    minHeight: '100%',
    overflow: 'hidden',
    boxSizing: 'border-box',
    color: '#e5edf7',
    backgroundColor: '#111f33',
    borderRight: '1px solid #24364d',
    transition: 'width 220ms cubic-bezier(0.2, 0, 0, 1), transform 220ms cubic-bezier(0.2, 0, 0, 1)',
    '@media (prefers-reduced-motion: reduce)': {
      transitionDuration: '0ms',
    },
  },
  drawerHeader: {
    display: 'flex',
    alignItems: 'center',
    gap: '11px',
    minHeight: '76px',
    padding: '0 20px',
    borderBottom: '1px solid rgb(219 232 248 / 9%)',
  },
  brandMark: {
    display: 'grid',
    width: '38px',
    height: '38px',
    flexShrink: 0,
    placeItems: 'center',
    border: '1px solid rgb(133 190 255 / 27%)',
    borderRadius: '12px',
    color: '#9bceff',
    background: 'linear-gradient(145deg, rgb(55 117 181 / 35%), rgb(28 69 113 / 35%))',
    boxShadow: '0 4px 12px rgb(0 0 0 / 12%)',
    '& svg': { width: '22px', height: '22px' },
  },
  brandText: {
    display: 'flex',
    flexDirection: 'column',
    gap: '3px',
    minWidth: 0,
  },
  brandTitle: {
    color: '#f5f8fc',
    fontSize: '16px',
    fontWeight: 700,
    letterSpacing: '-0.3px',
    lineHeight: 1.15,
  },
  brandSubtitle: {
    color: '#90a6c0',
    fontSize: '9px',
    fontWeight: 600,
    letterSpacing: '1.35px',
    lineHeight: 1.2,
  },
  workspaceCard: {
    display: 'flex',
    alignItems: 'center',
    gap: '11px',
    margin: '16px 12px 12px',
    padding: '12px 11px',
    border: '1px solid rgb(192 214 240 / 11%)',
    borderRadius: '10px',
    background: 'linear-gradient(120deg, rgb(255 255 255 / 6%), rgb(255 255 255 / 2%))',
  },
  workspaceMark: {
    display: 'grid',
    width: '34px',
    height: '34px',
    flexShrink: 0,
    placeItems: 'center',
    borderRadius: '9px',
    color: '#9ccaff',
    backgroundColor: 'rgb(81 143 205 / 16%)',
    '& svg': { width: '19px', height: '19px' },
  },
  workspaceText: {
    display: 'flex',
    flexDirection: 'column',
    gap: '4px',
    minWidth: 0,
  },
  workspaceName: {
    overflow: 'hidden',
    color: '#eaf1fa',
    fontSize: '12px',
    fontWeight: 600,
    textOverflow: 'ellipsis',
    whiteSpace: 'nowrap',
  },
  workspaceMeta: {
    color: '#8fa5bf',
    fontSize: '10px',
    letterSpacing: '0.15px',
  },
  drawerBody: {
    flex: 1,
    minHeight: 0,
    overflowY: 'auto',
    padding: '4px 10px 16px',
    scrollbarColor: '#3a506b transparent',
    scrollbarWidth: 'thin',
  },
  navGroup: {
    marginBottom: '15px',
    animationName: {
      from: { opacity: 0, transform: 'translateY(4px)' },
      to: { opacity: 1, transform: 'translateY(0)' },
    },
    animationDuration: '280ms',
    animationTimingFunction: 'ease-out',
    animationFillMode: 'both',
    '&:nth-child(2)': { animationDelay: '20ms' },
    '&:nth-child(3)': { animationDelay: '40ms' },
    '&:nth-child(4)': { animationDelay: '60ms' },
    '&:nth-child(5)': { animationDelay: '80ms' },
    '&:nth-child(6)': { animationDelay: '100ms' },
    '&:nth-child(7)': { animationDelay: '120ms' },
    '&:nth-child(8)': { animationDelay: '140ms' },
    '&:nth-child(9)': { animationDelay: '160ms' },
    '&:nth-child(10)': { animationDelay: '180ms' },
    '@media (prefers-reduced-motion: reduce)': {
      animationDuration: '0ms',
    },
  },
  sectionHeader: {
    display: 'flex',
    alignItems: 'center',
    gap: '8px',
    minHeight: '31px',
    margin: '3px 0 4px',
    padding: '0 10px',
    color: '#8198b3',
    fontSize: '9px',
    fontWeight: 700,
    letterSpacing: '1.05px',
    lineHeight: 1.2,
    textTransform: 'uppercase',
    '& svg': { width: '14px', height: '14px', color: '#7087a2' },
  },
  navItem: {
    position: 'relative',
    minHeight: '38px',
    margin: '2px 0',
    paddingLeft: '10px',
    borderRadius: '7px',
    color: '#b7c5d6',
    backgroundColor: 'transparent',
    fontSize: '12px',
    fontWeight: 500,
    transition: 'background-color 140ms ease, color 140ms ease, transform 140ms ease',
    '&:hover': {
      color: '#f2f6fb',
      backgroundColor: 'rgb(226 238 252 / 8%)',
      transform: 'translateX(2px)',
    },
    '&:focus-visible': {
      outline: '2px solid #8cc4ff',
      outlineOffset: '-2px',
    },
    '& [class*="icon"]': {
      color: '#91a7c0',
    },
    '@media (prefers-reduced-motion: reduce)': {
      transitionDuration: '0ms',
    },
  },
  navItemSelected: {
    color: '#f5f9ff',
    background: 'linear-gradient(100deg, rgb(52 119 188 / 33%), rgb(45 98 151 / 15%))',
    boxShadow: 'inset 2px 0 0 #78baff',
    fontWeight: 600,
    '&:hover': {
      color: '#fff',
      background: 'linear-gradient(100deg, rgb(58 132 207 / 40%), rgb(45 98 151 / 20%))',
    },
    '& [class*="icon"]': {
      color: '#9dceff',
    },
  },
  drawerFooter: {
    display: 'flex',
    alignItems: 'center',
    gap: '10px',
    margin: '0 14px',
    padding: '14px 4px 16px',
    borderTop: '1px solid rgb(219 232 248 / 9%)',
  },
  footerIcon: {
    display: 'grid',
    width: '30px',
    height: '30px',
    flexShrink: 0,
    placeItems: 'center',
    border: '1px solid rgb(105 185 154 / 22%)',
    borderRadius: '9px',
    color: '#82c6a9',
    backgroundColor: 'rgb(57 137 105 / 12%)',
    '& svg': { width: '16px', height: '16px' },
  },
  footerText: {
    display: 'flex',
    flexDirection: 'column',
    gap: '3px',
  },
  footerTitle: {
    color: '#c4d2e1',
    fontSize: '10px',
    fontWeight: 600,
  },
  footerSubtitle: {
    color: '#7f95ae',
    fontSize: '9px',
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
          <div className={styles.drawerHeader}>
            <div className={styles.brandMark} aria-hidden="true">
              <ShieldCheckmark20Regular />
            </div>
            <div className={styles.brandText}>
              <Text className={styles.brandTitle}>EDM 2.0</Text>
              <Text className={styles.brandSubtitle}>EXPENSE GOVERNANCE</Text>
            </div>
          </div>
          <div className={styles.workspaceCard}>
            <div className={styles.workspaceMark} aria-hidden="true">
              <Building20Regular />
            </div>
            <div className={styles.workspaceText}>
              <Text className={styles.workspaceName}>Warmbytes (Pvt) Ltd</Text>
              <Text className={styles.workspaceMeta}>Enterprise workspace</Text>
            </div>
          </div>
          <NavDrawerBody className={styles.drawerBody}>
            <Nav>
              {groups.map((group) => (
                <div key={group.area} className={styles.navGroup}>
                  <NavSectionHeader className={styles.sectionHeader}>
                    {areaIcons[group.area]}
                    {group.area}
                  </NavSectionHeader>
                  {group.items.map((item) => (
                    <NavItem
                      key={item.id}
                      value={item.id}
                      href={item.route}
                      icon={itemIcons[item.id] ?? <AppsList20Regular />}
                      className={item.id === selected ? styles.navItemSelected : styles.navItem}
                      aria-current={item.id === selected ? 'page' : undefined}
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
          <div className={styles.drawerFooter}>
            <div className={styles.footerIcon} aria-hidden="true">
              <LockClosed20Regular />
            </div>
            <div className={styles.footerText}>
              <Text className={styles.footerTitle}>Secure workspace</Text>
              <Text className={styles.footerSubtitle}>Governance you can trust</Text>
            </div>
          </div>
        </NavDrawer>

        <main id="main-content" className={styles.content} tabIndex={-1}>
          <Outlet />
        </main>
      </div>
    </div>
  );
}
