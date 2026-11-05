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
  Textarea,
} from '@fluentui/react-components';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useState, type FormEvent } from 'react';
import { ApiError } from '../api/apiError';
import { saveDeptHeadLimit, type DeptHeadLimitSummary, type LimitNature } from './configurationApi';
import {
  firstFieldMessages,
  formatPkr,
  karachiToday,
  messageFor,
  normaliseDescription,
  parseAmount,
  validateDeptHeadLimit,
  type FieldMessages,
} from './configurationValidation';
import styles from './ConfigurationPage.module.css';

/**
 * Set a new department-head limit. The active limit, if any, ends on the chosen effective date and is kept as history.
 * Validation runs on submit. Server field errors appear on the field. A changed limit (EDM-CONC-001) is a form message.
 */
export function DeptHeadLimitDialog({
  nature,
  summary,
  onClose,
}: {
  readonly nature: LimitNature;
  readonly summary: DeptHeadLimitSummary;
  readonly onClose: () => void;
}) {
  const queryClient = useQueryClient();
  const today = karachiToday();
  const [amount, setAmount] = useState('');
  const [description, setDescription] = useState('');
  const [effectiveDate, setEffectiveDate] = useState(today);
  const [submitted, setSubmitted] = useState(false);
  const [serverErrors, setServerErrors] = useState<FieldMessages>({});
  const [formError, setFormError] = useState<string | null>(null);

  const clientErrors = validateDeptHeadLimit({ amount, description, effectiveDate, today });
  const messages: FieldMessages = submitted ? { ...clientErrors, ...serverErrors } : serverErrors;

  const save = useMutation({
    mutationFn: () =>
      saveDeptHeadLimit({
        nature,
        amountPkr: parseAmount(amount),
        description: normaliseDescription(description),
        effectiveDate,
        rowVersion: summary.active?.rowVersion ?? null,
      }),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['configuration', 'dept-head-limit'] });
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

  const explanation = summary.active
    ? `The current limit of ${formatPkr(summary.active.amountPkr)} ends on the effective date you choose.`
    : `No limit is set yet. The default of ${formatPkr(summary.defaultAmountPkr)} applies until the new limit starts.`;

  return (
    <Dialog open modalType="modal" onOpenChange={(_, data) => !data.open && onClose()}>
      <DialogSurface>
        <form onSubmit={submit} noValidate>
          <DialogBody>
            <DialogTitle>Set {nature} department-head limit</DialogTitle>
            <DialogContent className={styles.form}>
              <Text>{explanation}</Text>
              <Field
                label="Limit (PKR)"
                required
                hint="Requests at or below this amount go to the Department Head only."
                validationMessage={messages.amountPkr}
                validationState={messages.amountPkr ? 'error' : 'none'}
              >
                <Input
                  value={amount}
                  inputMode="decimal"
                  autoComplete="off"
                  onChange={(_, data) => setAmount(data.value)}
                />
              </Field>
              <Field
                label="Effective date"
                required
                validationMessage={messages.effectiveDate}
                validationState={messages.effectiveDate ? 'error' : 'none'}
              >
                <Input
                  type="date"
                  value={effectiveDate}
                  min={today}
                  onChange={(_, data) => setEffectiveDate(data.value)}
                />
              </Field>
              <Field
                label="Description"
                validationMessage={messages.description}
                validationState={messages.description ? 'error' : 'none'}
              >
                <Textarea value={description} onChange={(_, data) => setDescription(data.value)} resize="vertical" />
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
                {save.isPending ? 'Saving' : 'Save limit'}
              </Button>
            </DialogActions>
          </DialogBody>
        </form>
      </DialogSurface>
    </Dialog>
  );
}
