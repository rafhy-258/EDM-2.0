import {
  Button,
  Dialog,
  DialogActions,
  DialogBody,
  DialogContent,
  DialogSurface,
  DialogTitle,
} from '@fluentui/react-components';
import type { ReactNode } from 'react';

/**
 * Confirmation for sensitive actions: deactivate, delete, approve, reject, revert (Part 14.2).
 * Focus returns to the trigger when the dialog closes (Fluent Dialog behaviour).
 */
export interface ConfirmDialogProps {
  readonly open: boolean;
  readonly title: string;
  readonly description: ReactNode;
  readonly confirmLabel: string;
  readonly cancelLabel?: string;
  /** Destructive actions use the danger style on the confirm button. */
  readonly destructive?: boolean;
  readonly busy?: boolean;
  readonly onConfirm: () => void;
  readonly onCancel: () => void;
}

export function ConfirmDialog({
  open,
  title,
  description,
  confirmLabel,
  cancelLabel = 'Cancel',
  destructive = false,
  busy = false,
  onConfirm,
  onCancel,
}: ConfirmDialogProps) {
  return (
    <Dialog open={open} modalType="modal" onOpenChange={(_, data) => !data.open && onCancel()}>
      <DialogSurface>
        <DialogBody>
          <DialogTitle>{title}</DialogTitle>
          <DialogContent>{description}</DialogContent>
          <DialogActions>
            <Button appearance="secondary" onClick={onCancel} disabled={busy}>
              {cancelLabel}
            </Button>
            <Button
              appearance="primary"
              onClick={onConfirm}
              disabled={busy}
              style={destructive ? { backgroundColor: 'var(--colorPaletteRedBackground3)' } : undefined}
            >
              {confirmLabel}
            </Button>
          </DialogActions>
        </DialogBody>
      </DialogSurface>
    </Dialog>
  );
}
