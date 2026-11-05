import {
  Button,
  Dialog,
  DialogActions,
  DialogBody,
  DialogContent,
  DialogSurface,
  DialogTitle,
  Dropdown,
  Field,
  Input,
  Option,
  Text,
} from '@fluentui/react-components';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useState, type FormEvent } from 'react';
import { ApiError } from '../api/apiError';
import styles from '../configuration/ConfigurationPage.module.css';
import { messageFor } from '../configuration/configurationValidation';
import {
  createDirectoryUser,
  updateDirectoryUser,
  type DepartmentOption,
  type DirectoryUserRecord,
} from './organisationApi';
import {
  normaliseEmail,
  organisationFieldMessages,
  validateDirectoryUser,
  type OrganisationFieldMessages,
} from './organisationValidation';

/**
 * Add a person to the directory or edit one. The department is required and must be active. The employee ID is
 * unique, including for people who are inactive, so a returning person is reactivated instead of added again.
 */
export function DirectoryUserDialog({
  person,
  departments,
  onClose,
}: {
  /** null adds a person; a record edits it. */
  readonly person: DirectoryUserRecord | null;
  readonly departments: readonly DepartmentOption[];
  readonly onClose: () => void;
}) {
  const queryClient = useQueryClient();
  const [employeeId, setEmployeeId] = useState(person?.employeeId ?? '');
  const [name, setName] = useState(person?.name ?? '');
  const [email, setEmail] = useState(person?.email ?? '');
  const [departmentId, setDepartmentId] = useState(person?.departmentId ?? '');
  const [submitted, setSubmitted] = useState(false);
  const [serverErrors, setServerErrors] = useState<OrganisationFieldMessages>({});
  const [formError, setFormError] = useState<string | null>(null);

  const clientErrors = validateDirectoryUser({ employeeId, name, email, departmentId });
  const messages: OrganisationFieldMessages = submitted ? { ...clientErrors, ...serverErrors } : serverErrors;
  const departmentLabel = departments.find((d) => d.id === departmentId)?.name ?? '';

  const save = useMutation({
    mutationFn: () => {
      const input = {
        employeeId: employeeId.trim(),
        name: name.trim(),
        email: normaliseEmail(email),
        departmentId,
      };
      return person
        ? updateDirectoryUser(person.id, { ...input, isActive: person.isActive, rowVersion: person.rowVersion })
        : createDirectoryUser(input);
    },
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['organisation'] });
      onClose();
    },
    onError: (error) => {
      setServerErrors(error instanceof ApiError ? organisationFieldMessages(error.fieldErrors) : {});
      setFormError(messageFor(error));
    },
  });

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setSubmitted(true);
    setServerErrors({});
    setFormError(null);
    if (Object.keys(clientErrors).length > 0) {
      return;
    }
    save.mutate();
  }

  return (
    <Dialog open modalType="modal" onOpenChange={(_, data) => !data.open && onClose()}>
      <DialogSurface>
        <form onSubmit={submit} noValidate>
          <DialogBody>
            <DialogTitle>{person ? 'Edit person' : 'Add person'}</DialogTitle>
            <DialogContent className={styles.form}>
              <Field
                label="Employee ID"
                required
                validationMessage={messages.employeeId}
                validationState={messages.employeeId ? 'error' : 'none'}
              >
                <Input value={employeeId} onChange={(_, data) => setEmployeeId(data.value)} autoComplete="off" />
              </Field>
              <Field
                label="Name"
                required
                validationMessage={messages.name}
                validationState={messages.name ? 'error' : 'none'}
              >
                <Input value={name} onChange={(_, data) => setName(data.value)} autoComplete="off" />
              </Field>
              <Field
                label="Email"
                validationMessage={messages.email}
                validationState={messages.email ? 'error' : 'none'}
              >
                <Input value={email} onChange={(_, data) => setEmail(data.value)} autoComplete="off" />
              </Field>
              <Field
                label="Department"
                required
                validationMessage={messages.departmentId}
                validationState={messages.departmentId ? 'error' : 'none'}
              >
                <Dropdown
                  aria-label="Department"
                  placeholder="Choose a department"
                  value={departmentLabel}
                  selectedOptions={departmentId ? [departmentId] : []}
                  onOptionSelect={(_, data) => setDepartmentId(data.optionValue ?? '')}
                >
                  {departments.map((d) => (
                    <Option key={d.id} value={d.id} text={d.name}>
                      {d.name}
                    </Option>
                  ))}
                </Dropdown>
              </Field>
              {formError ? (
                <Text role="alert" className={styles.error}>
                  {formError}
                </Text>
              ) : null}
            </DialogContent>
            <DialogActions>
              <Button appearance="secondary" onClick={onClose} disabled={save.isPending}>
                Cancel
              </Button>
              <Button type="submit" appearance="primary" disabled={save.isPending}>
                {save.isPending ? 'Saving' : 'Save'}
              </Button>
            </DialogActions>
          </DialogBody>
        </form>
      </DialogSurface>
    </Dialog>
  );
}
