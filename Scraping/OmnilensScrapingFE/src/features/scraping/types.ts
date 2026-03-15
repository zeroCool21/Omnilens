export interface ScrapingJob {
  id: string;
  target: string;
  status: 'pending' | 'running' | 'success' | 'error';
  updatedAt: string;
  durationMs: number;
}

export interface ScrapingSummary {
  successRate: number;
  averageDurationMs: number;
  lastRunAt: string;
}
