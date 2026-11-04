/**
 * Date and time display (Master Prompt, ADR-0008). Timestamps are stored in UTC and shown in
 * Asia/Karachi as dd-MMM-yyyy. Asia/Karachi has no daylight saving time, so the fixed offset
 * used for filter boundaries (+05:00) is exact.
 */
export const DISPLAY_TIME_ZONE = 'Asia/Karachi';
export const KARACHI_UTC_OFFSET = '+05:00';

const dateFormatter = new Intl.DateTimeFormat('en-GB', {
  timeZone: DISPLAY_TIME_ZONE,
  day: '2-digit',
  month: 'short',
  year: 'numeric',
});

const timeFormatter = new Intl.DateTimeFormat('en-GB', {
  timeZone: DISPLAY_TIME_ZONE,
  hour: '2-digit',
  minute: '2-digit',
  second: '2-digit',
  hourCycle: 'h23',
});

function partsOf(formatter: Intl.DateTimeFormat, date: Date): Record<string, string> {
  const result: Record<string, string> = {};
  for (const part of formatter.formatToParts(date)) {
    result[part.type] = part.value;
  }
  return result;
}

/** 04-Nov-2026 for an instant, in Asia/Karachi. */
export function formatKarachiDate(instant: Date): string {
  const p = partsOf(dateFormatter, instant);
  return `${p['day'] ?? ''}-${p['month'] ?? ''}-${p['year'] ?? ''}`;
}

/** 10:00:00 for an instant, in Asia/Karachi (24-hour clock). */
export function formatKarachiTime(instant: Date): string {
  const p = partsOf(timeFormatter, instant);
  return `${p['hour'] ?? ''}:${p['minute'] ?? ''}:${p['second'] ?? ''}`;
}

/** Start of a Karachi calendar day given as yyyy-mm-dd, as a UTC ISO string. */
export function karachiStartOfDayIso(day: string): string {
  return new Date(`${day}T00:00:00.000${KARACHI_UTC_OFFSET}`).toISOString();
}

/** End of a Karachi calendar day given as yyyy-mm-dd, as a UTC ISO string (inclusive). */
export function karachiEndOfDayIso(day: string): string {
  return new Date(`${day}T23:59:59.999${KARACHI_UTC_OFFSET}`).toISOString();
}
