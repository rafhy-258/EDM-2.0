import type { Permission } from '../session/permissions';
import { NAV_AREAS, NAVIGATION, type NavArea, type NavItemDef } from './navigation';

/**
 * Pure function: which Phase 1 items a user may see.
 * P2 items are removed here as well as in the data, so no Phase 2 item can appear by mistake.
 */
export function visibleNavigation(
  items: readonly NavItemDef[],
  hasPermission: (permission: Permission | null) => boolean,
): NavItemDef[] {
  return items.filter((item) => {
    if (item.phase !== 'P1') return false;
    if (item.requiredAnyPermission) {
      return item.requiredAnyPermission.some((permission) => hasPermission(permission));
    }
    return hasPermission(item.requiredPermission);
  });
}

export interface NavGroup {
  readonly area: NavArea;
  readonly items: readonly NavItemDef[];
}

/** Groups visible items by area, keeping the area order of Part 14.1. Empty areas are dropped. */
export function groupByArea(items: readonly NavItemDef[]): NavGroup[] {
  return NAV_AREAS.map((area) => ({
    area,
    items: items.filter((item) => item.area === area),
  })).filter((group) => group.items.length > 0);
}

/** The complete Phase 1 menu for a given permission check, grouped for rendering. */
export function menuFor(hasPermission: (permission: Permission | null) => boolean): NavGroup[] {
  return groupByArea(visibleNavigation(NAVIGATION, hasPermission));
}
