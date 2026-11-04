import { fireEvent, render, screen } from '@testing-library/react';
import { FluentProvider, webLightTheme } from '@fluentui/react-components';
import { describe, expect, it, vi } from 'vitest';
import { ConfirmDialog } from './ConfirmDialog';

function renderDialog(onConfirm = vi.fn(), onCancel = vi.fn()) {
  render(
    <FluentProvider theme={webLightTheme}>
      <ConfirmDialog
        open
        title="Deactivate category?"
        description="New requests can no longer select this category."
        confirmLabel="Deactivate"
        destructive
        onConfirm={onConfirm}
        onCancel={onCancel}
      />
    </FluentProvider>,
  );
  return { onConfirm, onCancel };
}

describe('ConfirmDialog', () => {
  it('names the action and explains the effect', () => {
    renderDialog();
    expect(screen.getByRole('dialog', { name: 'Deactivate category?' })).toBeInTheDocument();
    expect(screen.getByText('New requests can no longer select this category.')).toBeInTheDocument();
  });

  it('calls onConfirm only when the confirm button is pressed', () => {
    const { onConfirm, onCancel } = renderDialog();
    fireEvent.click(screen.getByRole('button', { name: 'Deactivate' }));
    expect(onConfirm).toHaveBeenCalledTimes(1);
    expect(onCancel).not.toHaveBeenCalled();
  });

  it('calls onCancel from the cancel button', () => {
    const { onConfirm, onCancel } = renderDialog();
    fireEvent.click(screen.getByRole('button', { name: 'Cancel' }));
    expect(onCancel).toHaveBeenCalledTimes(1);
    expect(onConfirm).not.toHaveBeenCalled();
  });
});
