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
  Select,
  Switch,
  Text,
  Textarea,
} from '@fluentui/react-components';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useState, type FormEvent } from 'react';
import { ApiError } from '../api/apiError';
import {
  createSubcategory,
  updateSubcategory,
  type CategoryRecord,
  type SubcategoryRecord,
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
 * Create or edit a subcategory. A new subcategory needs an active category. The category cannot change on edit.
 */
export function SubcategoryDialog({
  subcategory,
  activeCategories,
  defaultCategoryId,
  onClose,
}: {
  /** null creates a subcategory; a record edits it. */
  readonly subcategory: SubcategoryRecord | null;
  readonly activeCategories: readonly CategoryRecord[];
  /** Pre-selects the category chosen in the page filter. */
  readonly defaultCategoryId: string | null;
  readonly onClose: () => void;
}) {
  const queryClient = useQueryClient();
  const [categoryId, setCategoryId] = useState(subcategory?.categoryId ?? defaultCategoryId ?? '');
  const [name, setName] = useState(subcategory?.name ?? '');
  const [description, setDescription] = useState(subcategory?.description ?? '');
  const [isActive, setIsActive] = useState(subcategory?.isActive ?? true);
  const [submitted, setSubmitted] = useState(false);
  const [serverErrors, setServerErrors] = useState<FieldMessages>({});
  const [formError, setFormError] = useState<string | null>(null);

  const clientErrors = validateNameAndDescription({ name, description });
  const categoryError = !subcategory && categoryId === '' ? 'Choose a category.' : undefined;
  const messages: FieldMessages = submitted
    ? { ...clientErrors, ...(categoryError ? { categoryId: categoryError } : {}), ...serverErrors }
    : serverErrors;

  const save = useMutation({
    mutationFn: () => {
      const trimmedName = name.trim();
      const normalised = normaliseDescription(description);
      return subcategory
        ? updateSubcategory(subcategory.id, {
            name: trimmedName,
            description: normalised,
            isActive,
            rowVersion: subcategory.rowVersion,
          })
        : createSubcategory({ categoryId, name: trimmedName, description: normalised });
    },
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['configuration', 'subcategories'] });
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
    if (Object.keys(clientErrors).length > 0 || categoryError) {
      return;
    }
    save.mutate();
  }

  const categoryOptions = subcategory
    ? [{ id: subcategory.categoryId, name: subcategory.categoryName }]
    : activeCategories.map((c) => ({ id: c.id, name: c.name }));

  return (
    <Dialog open modalType="modal" onOpenChange={(_, data) => !data.open && onClose()}>
      <DialogSurface>
        <form onSubmit={submit} noValidate>
          <DialogBody>
            <DialogTitle>{subcategory ? 'Edit subcategory' : 'New subcategory'}</DialogTitle>
            <DialogContent className={styles.form}>
              <Field
                label="Category"
                required
                validationMessage={messages.categoryId}
                validationState={messages.categoryId ? 'error' : 'none'}
              >
                <Select
                  value={categoryId}
                  disabled={subcategory !== null}
                  onChange={(_, data) => setCategoryId(data.value)}
                >
                  {subcategory ? null : <option value="">Choose a category</option>}
                  {categoryOptions.map((option) => (
                    <option key={option.id} value={option.id}>
                      {option.name}
                    </option>
                  ))}
                </Select>
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
                label="Description"
                validationMessage={messages.description}
                validationState={messages.description ? 'error' : 'none'}
              >
                <Textarea value={description} onChange={(_, data) => setDescription(data.value)} resize="vertical" />
              </Field>
              {subcategory ? (
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
