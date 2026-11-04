import { describe, expect, it } from 'vitest';
import { toSessionUser, type ServerSession } from './sessionApi';

const SERVER: ServerSession = {
  userId: '5b0a1c2d-0000-4000-8000-000000000042',
  username: 'sara.khan',
  displayName: 'Sara Khan',
  department: 'Finance',
  roles: ['AUDITOR'],
  permissions: ['audit.view', 'made.up.permission'],
};

describe('toSessionUser', () => {
  it('keeps only the permissions this client knows about', () => {
    const user = toSessionUser(SERVER);

    expect([...user.permissions]).toEqual(['audit.view']);
    expect(user.displayName).toBe('Sara Khan');
    expect(user.department).toBe('Finance');
  });

  it('labels a missing department rather than leaving it blank', () => {
    expect(toSessionUser({ ...SERVER, department: null }).department).toBe('No department');
  });
});
