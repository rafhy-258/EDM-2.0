/**
 * Design tokens for layout and spacing (ADR-0003).
 * Colour comes from Fluent UI themes. Tokens here cover layout sizes, breakpoints and spacing,
 * so that Tailwind layout classes and Fluent components use one set of values.
 */
export const layout = {
  sidebarWidthPx: 280,
  headerHeightPx: 56,
  contentMaxWidthPx: 1440,
  /** Below this width the navigation becomes an overlay drawer (tablet and mobile). */
  drawerBreakpointPx: 1024,
} as const;

export const spacing = {
  xs: '4px',
  sm: '8px',
  md: '12px',
  lg: '16px',
  xl: '24px',
  xxl: '32px',
} as const;

export const focus = {
  /** Visible focus ring. Keyboard users must always see where focus is (WCAG 2.4.7). */
  outlineWidthPx: 2,
} as const;
