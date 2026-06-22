const timestampFormatter = new Intl.DateTimeFormat(undefined, {
  dateStyle: 'medium',
  timeStyle: 'short',
})

const relativeFormatter = new Intl.RelativeTimeFormat(undefined, {
  numeric: 'auto',
})

export function formatTimestamp(value: Date | string | number): string {
  const date = new Date(value)

  if (Number.isNaN(date.getTime())) {
    return 'Invalid date'
  }

  return timestampFormatter.format(date)
}

export function formatRelativeTime(value: Date | string | number, now: Date = new Date()): string {
  const date = new Date(value)

  if (Number.isNaN(date.getTime())) {
    return 'Invalid date'
  }

  const diffSeconds = Math.round((date.getTime() - now.getTime()) / 1_000)
  const absSeconds = Math.abs(diffSeconds)

  if (absSeconds < 60) {
    return relativeFormatter.format(diffSeconds, 'second')
  }

  const diffMinutes = Math.round(diffSeconds / 60)
  if (Math.abs(diffMinutes) < 60) {
    return relativeFormatter.format(diffMinutes, 'minute')
  }

  const diffHours = Math.round(diffMinutes / 60)
  if (Math.abs(diffHours) < 24) {
    return relativeFormatter.format(diffHours, 'hour')
  }

  const diffDays = Math.round(diffHours / 24)
  return relativeFormatter.format(diffDays, 'day')
}
