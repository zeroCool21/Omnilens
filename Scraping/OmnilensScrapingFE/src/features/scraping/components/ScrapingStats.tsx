import { useScrapingSummary } from '../hooks/useScrapingJobs';
import { formatDistanceToNow } from '../../../shared/utils/date';

const formatPercentage = (value: number) => (value * 100).toFixed(1) + '%';

export function ScrapingStats() {
  const { data, isLoading, isError } = useScrapingSummary();

  if (isLoading) {
    return <p className="text-sm text-slate-500">Calcolo delle statistiche...</p>;
  }

  if (isError || !data) {
    return <p className="text-sm text-rose-600">Statistiche non disponibili.</p>;
  }

  return (
    <div className="space-y-4 rounded-lg border border-slate-100 bg-white p-4 shadow-sm">
      <header className="space-y-1">
        <h3 className="text-base font-semibold text-slate-800">Performance</h3>
        <p className="text-xs text-slate-500">
          Ultimo aggiornamento {formatDistanceToNow(data.lastRunAt)}
        </p>
      </header>
      <dl className="space-y-3 text-sm text-slate-700">
        <div className="flex items-center justify-between">
          <dt>Tasso di successo</dt>
          <dd className="font-semibold">{formatPercentage(data.successRate)}</dd>
        </div>
        <div className="flex items-center justify-between">
          <dt>Durata media</dt>
          <dd className="font-semibold">{Math.round(data.averageDurationMs / 1000)}s</dd>
        </div>
      </dl>
    </div>
  );
}
