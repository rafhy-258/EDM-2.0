import { render, screen } from '@testing-library/react';
import { FluentProvider, webLightTheme } from '@fluentui/react-components';
import { describe, expect, it } from 'vitest';
import { StatusBadge } from './StatusBadge';

describe('StatusBadge', () => {
  it('shows the text label, so status is not conveyed by colour alone', () => {
    render(
      <FluentProvider theme={webLightTheme}>
        <StatusBadge status="CLOSED" />
      </FluentProvider>,
    );
    expect(screen.getByText('Closed (Quorum Met)')).toBeInTheDocument();
  });
});
