import {
  Button,
  Dialog,
  DialogActions,
  DialogBody,
  DialogContent,
  DialogSurface,
  DialogTitle,
  Field,
  Input,
  Text,
} from '@fluentui/react-components';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useState, type FormEvent } from 'react';
import { ApiError } from '../api/apiError';
import styles from '../configuration/ConfigurationPage.module.css';
import { messageFor } from '../configuration/configurationValidation';
import { createDepartment, updateDepartment, type DepartmentRecord } from './organisationApi';
import {
  organisationFieldMessages,
  validateDepartmentName,
  type OrganisationFieldMessages,
} from './organisationValidation';

/**
 * Create or rename a department. A department is deactivated from the list, not here. A duplicate name is shown on the
 * field. A stale version (EDM-CONC-001) is shown as a form message.
 */
export function DepartmentDialog({
  department,
  onClose,
}: {
  /** null creates a department; a record renames it. */
  readonly department: DepartmentRecord | null;
  readonly onClose: () => void;
}) {
  const queryClient = useQueryClient();
  const [name, setName] = useState(department?.name ?? '');
  const [submitted, setSubmitted] = useState(false);
  const [serverErrors, setServerErrors] = useState<OrganisationFieldMessages>({});
  const [formError, setFormError] = useState<string | null>(null);

  const clientNameError = validateDepartmentName(name);
  const messages: OrganisationFieldMessages =
    submitted && clientNameError ? { ...serverErrors, name: clientNameError } : serverErrors;

  const save = useMutation({
    mutationFn: () =>
      department
        ? updateDepartment(department.id, {
            name: name.trim(),
            isActive: department.isActive,
            rowVersion: department.rowVersion,
          })
        : createDepartment({ name: name.trim() }),
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
    if (clientNameError) {
      return;
    }
    save.mutate();
  }

  return (
    <Dialog open modalType="modal" onOpenChange={(_, data) => !data.open && onClose()}>
      <DialogSurface>
        <form onSubmit={submit} noValidate>
          <DialogBody>
            <DialogTitle>{department ? 'Rename department' : 'New department'}</DialogTitle>
            <DialogContent className={styles.form}>
              <Field
                label="Name"
                required
                validationMessage={messages.name}
                validationState={messages.name ? 'error' : 'none'}
              >
                <Input value={name} onChange={(_, data) => setName(data.value)} autoComplete="off" />
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
