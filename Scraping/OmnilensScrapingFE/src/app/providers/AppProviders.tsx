import type { PropsWithChildren } from 'react';
import { BrowserRouter } from 'react-router-dom';
import { ReactQueryProvider } from './ReactQueryProvider';

export function AppProviders({ children }: PropsWithChildren) {
  return (
    <ReactQueryProvider>
      <BrowserRouter>{children}</BrowserRouter>
    </ReactQueryProvider>
  );
}
