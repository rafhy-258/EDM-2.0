import { describe, expect, it } from 'vitest';
import {
  normaliseEmail,
  organisationFieldMessages,
  validateDepartmentName,
  validateDirectoryUser,
} from './organisationValidation';

const valid = { employeeId: 'EMP1024', name: 'Ali Khan', email: '', departmentId: 'd-1' };

describe('department name', () => {
  it('requires a name after trimming', () => {
    expect(validateDepartmentName('   ')).toBe('Enter a department name.');
  });

  it('accepts up to 200 characters and refuses more', () => {
    expect(validateDepartmentName('a'.repeat(200))).toBeUndefined();
    expect(validateDepartmentName('a'.repeat(201))).toBe('The department name must be 200 characters or fewer.');
  });
});

describe('directory person', () => {
  it('accepts a valid person with no email', () => {
    expect(validateDirectoryUser(valid)).toEqual({});
  });

  it('allows letters and numbers only in the employee ID', () => {
    expect(validateDirectoryUser({ ...valid, employeeId: 'EMP-1' }).employeeId).toBe(
      'Use letters and numbers only, for example EMP1024.',
    );
  });

  it('refuses an email that is not valid, but allows an empty one', () => {
    expect(validateDirectoryUser({ ...valid, email: 'ali@example' }).email).toBe('Enter a valid email address.');
    expect(validateDirectoryUser({ ...valid, email: '' }).email).toBeUndefined();
  });

  it('requires a department', () => {
    expect(validateDirectoryUser({ ...valid, departmentId: '' }).departmentId).toBe('Choose a department.');
  });
});

describe('server field errors', () => {
  it('keeps the first message for each known field', () => {
    expect(
      organisationFieldMessages({
        employeeId: ['This employee ID already exists. Reactivate the existing record instead.'],
        isActive: ['Move or deactivate the people in this department first.'],
        unknown: ['ignored'],
      }),
    ).toEqual({
      employeeId: 'This employee ID already exists. Reactivate the existing record instead.',
      isActive: 'Move or deactivate the people in this department first.',
    });
  });
});

describe('email storage', () => {
  it('is trimmed, and null when empty', () => {
    expect(normaliseEmail('  ali@example.com ')).toBe('ali@example.com');
    expect(normaliseEmail('   ')).toBeNull();
  });
});
