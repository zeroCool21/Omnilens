import { Suspense } from 'react';
import { ScrapingJobList } from '../../features/scraping/components/ScrapingJobList';
import { ScrapingStats } from '../../features/scraping/components/ScrapingStats';
import { AppLayout } from '../../shared/components/layout/AppLayout';

export function DashboardPage() {
  return (
    <AppLayout title="Omnilens Scraping">
      <div className="grid gap-6 md:grid-cols-[2fr,1fr]">
        <section className="space-y-4">
          <header>
            <h2 className="text-lg font-semibold text-slate-800">Jobs recenti</h2>
            <p className="text-sm text-slate-500">
              Stato dei job di scraping e ultime esecuzioni.
            </p>
          </header>
          <Suspense fallback={<p className="text-sm text-slate-500">Caricamento...</p>}>
            <ScrapingJobList />
          </Suspense>
        </section>
        <aside className="space-y-4">
          <Suspense fallback={<p className="text-sm text-slate-500">Caricamento...</p>}>
            <ScrapingStats />
          </Suspense>
        </aside>
      </div>
    </AppLayout>
  );
}
