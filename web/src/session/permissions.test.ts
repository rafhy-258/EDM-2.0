import { describe, expect, it } from 'vitest';
import { PERMISSIONS } from './permissions';

describe('permission catalogue (Part 20.1)', () => {
  it('has exactly the 49 permission strings of Part 20.1', () => {
    expect(PERMISSIONS).toHaveLength(49);
  });

  it('has no duplicates', () => {
    expect(new Set(PERMISSIONS).size).toBe(PERMISSIONS.length);
  });

  it('uses lowercase dotted names only', () => {
    for (const p of PERMISSIONS) {
      expect(p, p).toMatch(/^[a-z]+(\.[a-z]+)+$/);
    }
  });

  it('includes the named approval and matrix permissions the engine depends on', () => {
    expect(PERMISSIONS).toEqual(expect.arrayContaining(['approval.act', 'config.matrix.approve', 'audit.view']));
  });
});
