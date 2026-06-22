import { clsx, type ClassValue } from 'clsx'
import { twMerge } from 'tailwind-merge'

export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs))
}

export function formatExecutionMs(value: number): string {
  if (!Number.isFinite(value) || value < 0) {
    return 'Unknown duration'
  }

  if (value < 1_000) {
    return `${Math.round(value)} ms`
  }

  return `${(value / 1_000).toFixed(2)} s`
}

export function formatShardStats(successfulShards: number, totalShards: number): string {
  if (totalShards <= 0) {
    return 'No shard metadata'
  }

  return `${successfulShards}/${totalShards} shards successful`
}

export function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
}
