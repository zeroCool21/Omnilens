import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { App } from './App';

vi.mock('./providers/AppProviders', () => ({
  AppProviders: ({ children }: { children: React.ReactNode }) => <>{children}</>
}));

vi.mock('./routes/AppRoutes', () => ({
  AppRoutes: () => <div>Routes mock</div>
}));

describe('App', () => {
  it('renders routes inside providers', () => {
    render(<App />);
    expect(screen.getByText('Routes mock')).toBeInTheDocument();
  });
});
