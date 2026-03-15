import { apiClient } from '../../../shared/api/client';
import type { ScrapingJob, ScrapingSummary } from '../types';

export async function fetchScrapingJobs() {
  const data = await apiClient<ScrapingJob[]>({ path: '/scraping/jobs' });
  return data;
}

export async function fetchScrapingSummary() {
  const data = await apiClient<ScrapingSummary>({ path: '/scraping/summary' });
  return data;
}
