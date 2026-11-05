import { describe, expect, it } from 'vitest';
import { toApiError } from './apiError';

describe('API problem responses', () => {
  it('uses the RFC problem detail when a message is not provided', async () => {
    const response = new Response(
      JSON.stringify({
        title: 'The request could not be completed',
        detail: 'The departments table is unavailable.',
        status: 500,
      }),
      { status: 500, headers: { 'Content-Type': 'application/problem+json' } },
    );

    await expect(toApiError(response)).resolves.toMatchObject({
      status: 500,
      message: 'The departments table is unavailable.',
    });
  });

  it('prefers the EDM message and falls back to the problem title', async () => {
    const edmResponse = new Response(JSON.stringify({ message: 'The EDM message.' }), { status: 422 });
    const problemResponse = new Response(JSON.stringify({ title: 'A problem occurred.' }), { status: 500 });

    await expect(toApiError(edmResponse)).resolves.toMatchObject({ message: 'The EDM message.' });
    await expect(toApiError(problemResponse)).resolves.toMatchObject({ message: 'A problem occurred.' });
  });
});
