import { describe, expect, it } from 'vitest';
import { REQUEST_STATUS, STEP_STATE, statusDefinition } from './statuses';

describe('status labels and colours (Part 8.1)', () => {
  it('defines the request statuses of Part 8.1 (Draft is a Phase 2 proposal)', () => {
    expect(Object.keys(REQUEST_STATUS)).toEqual(['DRAFT', 'PENDING', 'REVERTED', 'APPROVED', 'REJECTED']);
    expect(REQUEST_STATUS.DRAFT.phase).toBe('P2');
  });

  it('defines the step states of Part 8.1 and 22.3', () => {
    expect(Object.keys(STEP_STATE)).toEqual([
      'WAITING',
      'ACTIVE',
      'APPROVED',
      'REJECTED',
      'REVERTED',
      'FORWARDED',
      'CLOSED',
      'SUPERSEDED',
    ]);
    expect(STEP_STATE.CLOSED.label).toBe('Closed (Quorum Met)');
  });

  it('gives every status a non-empty label', () => {
    for (const def of [...Object.values(REQUEST_STATUS), ...Object.values(STEP_STATE)]) {
      expect(def.label.trim().length).toBeGreaterThan(0);
    }
  });

  it('keeps the same colour for the same meaning across request and step families', () => {
    expect(REQUEST_STATUS.APPROVED.color).toBe(STEP_STATE.APPROVED.color);
    expect(REQUEST_STATUS.REJECTED.color).toBe(STEP_STATE.REJECTED.color);
    expect(REQUEST_STATUS.REVERTED.color).toBe(STEP_STATE.REVERTED.color);
  });

  it('resolves any key from either family', () => {
    expect(statusDefinition('PENDING').label).toBe('Pending');
    expect(statusDefinition('ACTIVE').label).toBe('Active');
  });
});
