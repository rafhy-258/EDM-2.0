/**
 * Client-side checks for the organisation forms. They mirror OrganisationRules on the server, so the user sees the
 * message before a round trip. The server checks again and remains the authority.
 */
export const DEPARTMENT_NAME_MAX_LENGTH = 200;
export const NAME_MAX_LENGTH = 200;
export const EMPLOYEE_ID_MAX_LENGTH = 32;
export const EMAIL_MAX_LENGTH = 254;

const EMPLOYEE_ID_PATTERN = /^[A-Za-z0-9]+$/;
const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

export type OrganisationFieldName = 'name' | 'employeeId' | 'email' | 'departmentId' | 'isActive';

export type OrganisationFieldMessages = Readonly<Partial<Record<OrganisationFieldName, string>>>;

export function validateDepartmentName(name: string): string | undefined {
  const trimmed = name.trim();
  if (trimmed.length === 0) {
    return 'Enter a department name.';
  }
  if (trimmed.length > DEPARTMENT_NAME_MAX_LENGTH) {
    return `The department name must be ${DEPARTMENT_NAME_MAX_LENGTH} characters or fewer.`;
  }
  return undefined;
}

export interface DirectoryUserFields {
  readonly employeeId: string;
  readonly name: string;
  readonly email: string;
  readonly departmentId: string;
}

/** Checks the person form. Email is optional. The employee ID is letters and numbers only. */
export function validateDirectoryUser(fields: DirectoryUserFields): OrganisationFieldMessages {
  const messages: Partial<Record<OrganisationFieldName, string>> = {};

  const employeeId = fields.employeeId.trim();
  if (employeeId.length === 0) {
    messages.employeeId = 'Enter an employee ID.';
  } else if (employeeId.length > EMPLOYEE_ID_MAX_LENGTH) {
    messages.employeeId = `The employee ID must be ${EMPLOYEE_ID_MAX_LENGTH} characters or fewer.`;
  } else if (!EMPLOYEE_ID_PATTERN.test(employeeId)) {
    messages.employeeId = 'Use letters and numbers only, for example EMP1024.';
  }

  const name = fields.name.trim();
  if (name.length === 0) {
    messages.name = 'Enter a name.';
  } else if (name.length > NAME_MAX_LENGTH) {
    messages.name = `The name must be ${NAME_MAX_LENGTH} characters or fewer.`;
  }

  const email = fields.email.trim();
  if (email.length > EMAIL_MAX_LENGTH) {
    messages.email = `The email must be ${EMAIL_MAX_LENGTH} characters or fewer.`;
  } else if (email.length > 0 && !EMAIL_PATTERN.test(email)) {
    messages.email = 'Enter a valid email address.';
  }

  if (fields.departmentId.length === 0) {
    messages.departmentId = 'Choose a department.';
  }

  return messages;
}

/** Field errors returned by the server (ApiError.fieldErrors), one message per field. */
export function organisationFieldMessages(
  fieldErrors: Readonly<Record<string, readonly string[]>>,
): OrganisationFieldMessages {
  const messages: Partial<Record<OrganisationFieldName, string>> = {};
  for (const key of ['name', 'employeeId', 'email', 'departmentId', 'isActive'] as const) {
    const first = fieldErrors[key]?.[0];
    if (first) {
      messages[key] = first;
    }
  }
  return messages;
}

/** The stored email: trimmed, and null when empty. */
export function normaliseEmail(email: string): string | null {
  const trimmed = email.trim();
  return trimmed.length === 0 ? null : trimmed;
}
