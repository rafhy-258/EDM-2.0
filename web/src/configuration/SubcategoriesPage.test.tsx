import { FluentProvider, webLightTheme } from '@fluentui/react-components';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { devSessionFor, SessionProvider } from '../session/session';
import { SubcategoriesPage } from './SubcategoriesPage';

const travel = {
  id: '6f1e2a10-0000-4000-8000-000000000001',
  name: 'Travel',
  description: null,
  isActive: true,
  rowVersion: 1,
  subcategoryCount: 1,
  updatedAtUtc: '2026-11-04T05:00:00Z',
  updatedBy: null,
};

const flights = {
  id: '7a2f3b20-0000-4000-8000-000000000002',
  categoryId: travel.id,
  categoryName: 'Travel',
  name: 'Flights',
  description: null,
  isActive: true,
  rowVersion: 1,
  updatedAtUtc: '2026-11-04T05:00:00Z',
  updatedBy: null,
};

function stubApi(categories: unknown[], subcategories: unknown[]) {
  const urls: string[] = [];
  vi.stubGlobal(
    'fetch',
    vi.fn((url: string) => {
      urls.push(url);
      const body = url.startsWith('/api/v1/categories') ? categories : subcategories;
      return Promise.resolve(new Response(JSON.stringify(body), { status: 200, headers: { 'Content-Type': 'application/json' } }));
    }),
  );
  return urls;
}

function renderPage() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <FluentProvider theme={webLightTheme}>
        <SessionProvider user={devSessionFor('EDM_ADMINISTRATOR')}>
          <SubcategoriesPage />
        </SessionProvider>
      </FluentProvider>
    </QueryClientProvider>,
  );
}

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('Subcategories screen', () => {
  it('lists subcategories with their category, and filters by category on the server', async () => {
    const urls = stubApi([travel], [flights]);
    renderPage();

    expect(await screen.findByText('Flights')).toBeInTheDocument();
    const rowCategory = screen.getAllByText('Travel').find((el) => el.closest('tr'));
    expect(rowCategory).toBeDefined();

    fireEvent.change(screen.getByRole('combobox', { name: 'Category' }), { target: { value: travel.id } });

    await waitFor(() =>
      expect(urls).toContain(`/api/v1/subcategories?categoryId=${travel.id}&includeInactive=false`),
    );
  });

  it('does not allow a new subcategory when there is no active category', async () => {
    stubApi([], []);
    renderPage();

    expect(await screen.findByText('Create an active category before adding subcategories.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'New subcategory' })).toBeDisabled();
  });

  it('asks for a category before a subcategory is saved', async () => {
    stubApi([travel], []);
    renderPage();

    const newButton = await screen.findByRole('button', { name: 'New subcategory' });
    await waitFor(() => expect(newButton).toBeEnabled());
    fireEvent.click(newButton);
    const dialog = screen.getByRole('dialog');
    fireEvent.change(within(dialog).getByLabelText(/Name/), { target: { value: 'Hotels' } });
    fireEvent.click(within(dialog).getByRole('button', { name: 'Save' }));

    expect(await within(dialog).findByText('Choose a category.')).toBeInTheDocument();
  });
});
