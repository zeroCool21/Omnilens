const relativeTime = new Intl.RelativeTimeFormat('it', { numeric: 'auto' });

export function formatDistanceToNow(value: string | number | Date) {
  const target = typeof value === 'string' || typeof value === 'number' ? new Date(value) : value;
  const now = new Date();
  const diffMs = target.getTime() - now.getTime();
  const diffMinutes = Math.round(diffMs / (1000 * 60));

  if (Math.abs(diffMinutes) < 60) {
    return relativeTime.format(diffMinutes, 'minute');
  }

  const diffHours = Math.round(diffMinutes / 60);
  if (Math.abs(diffHours) < 24) {
    return relativeTime.format(diffHours, 'hour');
  }

  const diffDays = Math.round(diffHours / 24);
  return relativeTime.format(diffDays, 'day');
}
