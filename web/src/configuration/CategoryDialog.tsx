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
  Switch,
  Text,
  Textarea,
} from '@fluentui/react-components';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useState, type FormEvent } from 'react';
import { ApiError } from '../api/apiError';
import {
  createCategory,
  updateCategory,
  type CategoryRecord,
} from './configurationApi';
import {
  firstFieldMessages,
  messageFor,
  normaliseDescription,
  validateNameAndDescription,
  type FieldMessages,
} from './configurationValidation';
import styles from './ConfigurationPage.module.css';

/**
 * Create or edit a category. Validation runs on submit. Server field errors (for example a duplicate name) are shown
 * on the field, and a stale version (EDM-CONC-001) is shown as a form message.
 */
export function CategoryDialog({
  category,
  onClose,
}: {
  /** null creates a category; a record edits it. */
  readonly category: CategoryRecord | null;
  readonly onClose: () => void;
}) {
  const queryClient = useQueryClient();
  const [name, setName] = useState(category?.name ?? '');
  const [description, setDescription] = useState(category?.description ?? '');
  const [isActive, setIsActive] = useState(category?.isActive ?? true);
  const [submitted, setSubmitted] = useState(false);
  const [serverErrors, setServerErrors] = useState<FieldMessages>({});
  const [formError, setFormError] = useState<string | null>(null);

  const clientErrors = validateNameAndDescription({ name, description });
  const messages: FieldMessages = submitted ? { ...clientErrors, ...serverErrors } : serverErrors;

  const save = useMutation({
    mutationFn: () => {
      const trimmedName = name.trim();
      const normalised = normaliseDescription(description);
      return category
        ? updateCategory(category.id, {
            name: trimmedName,
            description: normalised,
            isActive,
            rowVersion: category.rowVersion,
          })
        : createCategory({ name: trimmedName, description: normalised });
    },
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['configuration', 'categories'] });
      onClose();
    },
    onError: (error) => {
      setServerErrors(error instanceof ApiError ? firstFieldMessages(error.fieldErrors) : {});
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
            <DialogTitle>{category ? 'Edit category' : 'New category'}</DialogTitle>
            <DialogContent className={styles.form}>
              <Field
                label="Name"
                required
                validationMessage={messages.name}
                validationState={messages.name ? 'error' : 'none'}
              >
                <Input value={name} onChange={(_, data) => setName(data.value)} autoComplete="off" />
              </Field>
              <Field
                label="Description"
                validationMessage={messages.description}
                validationState={messages.description ? 'error' : 'none'}
              >
                <Textarea value={description} onChange={(_, data) => setDescription(data.value)} resize="vertical" />
              </Field>
              {category ? (
                <Switch label="Active" checked={isActive} onChange={(_, data) => setIsActive(data.checked)} />
              ) : null}
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
