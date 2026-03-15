import { Fragment } from 'react';
import { useScrapingJobs } from '../hooks/useScrapingJobs';
import type { ScrapingJob } from '../types';
import { formatDistanceToNow } from '../../../shared/utils/date';

const statusColor: Record<ScrapingJob['status'], string> = {
  pending: 'bg-amber-100 text-amber-700',
  running: 'bg-blue-100 text-blue-700',
  success: 'bg-emerald-100 text-emerald-700',
  error: 'bg-rose-100 text-rose-700'
};

export function ScrapingJobList() {
  const { data, isLoading, isError } = useScrapingJobs();

  if (isLoading) {
    return <p className="text-sm text-slate-500">Recupero dei job...</p>;
  }

  if (isError) {
    return <p className="text-sm text-rose-600">Impossibile caricare i job.</p>;
  }

  if (!data?.length) {
    return <p className="text-sm text-slate-500">Nessun job disponibile.</p>;
  }

  return (
    <ul className="divide-y divide-slate-100 rounded-lg border border-slate-100 bg-white shadow-sm">
      {data.map((job) => (
        <Fragment key={job.id}>
          <li className="flex items-center justify-between p-4">
            <div>
              <p className="text-sm font-medium text-slate-800">{job.target}</p>
              <p className="text-xs text-slate-500">
                Aggiornato {formatDistanceToNow(job.updatedAt)}
              </p>
            </div>
            <div className="flex flex-col items-end gap-2 text-xs">
              <span className={'rounded-full px-3 py-1 font-semibold ' + statusColor[job.status]}>
                {job.status}
              </span>
              <span className="text-slate-400">{Math.round(job.durationMs / 1000)}s</span>
            </div>
          </li>
        </Fragment>
      ))}
    </ul>
  );
}
