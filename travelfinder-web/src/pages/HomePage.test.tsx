import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { describe, expect, it } from 'vitest';
import { HomePage } from './HomePage';
import { Providers } from '../app/Providers';

describe('HomePage', () => {
  it('renders prompt and start without ion/ui5', () => {
    const { container } = render(
      <Providers>
        <MemoryRouter>
          <HomePage />
        </MemoryRouter>
      </Providers>,
    );
    expect(screen.getByRole('textbox')).toBeTruthy();
    expect(screen.getByRole('button', { name: /start planning/i })).toBeTruthy();
    expect(container.querySelector('ion-content')).toBeNull();
    expect(container.querySelector('ui5-button')).toBeNull();
  });
});
