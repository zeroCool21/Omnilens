import { useQuery } from '@tanstack/react-query';
import { fetchScrapingJobs, fetchScrapingSummary } from '../api/fetchScrapingJobs';

export function useScrapingJobs() {
  return useQuery({
    queryKey: ['scraping', 'jobs'],
    queryFn: fetchScrapingJobs
  });
}

export function useScrapingSummary() {
  return useQuery({
    queryKey: ['scraping', 'summary'],
    queryFn: fetchScrapingSummary
  });
}
