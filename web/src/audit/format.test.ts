import { describe, expect, it } from 'vitest';
import { formatKarachiDate, formatKarachiTime, karachiEndOfDayIso, karachiStartOfDayIso } from './format';

describe('Asia/Karachi display', () => {
  it('shows a UTC instant as dd-MMM-yyyy in Karachi, across the midnight boundary', () => {
    // 2026-11-03T20:00Z is already 04 Nov 01:00 in Karachi (UTC+5).
    const instant = new Date('2026-11-03T20:00:00Z');
    expect(formatKarachiDate(instant)).toBe('04-Nov-2026');
    expect(formatKarachiTime(instant)).toBe('01:00:00');
  });

  it('uses a 24-hour clock with two-digit fields', () => {
    expect(formatKarachiTime(new Date('2026-11-04T04:05:09Z'))).toBe('09:05:09');
    expect(formatKarachiTime(new Date('2026-11-04T18:30:00Z'))).toBe('23:30:00');
  });

  it('zero-pads the day', () => {
    expect(formatKarachiDate(new Date('2026-01-05T06:00:00Z'))).toBe('05-Jan-2026');
  });
});

describe('Karachi day bounds for filters', () => {
  it('starts the day at 00:00 Karachi, which is the previous day in UTC', () => {
    expect(karachiStartOfDayIso('2026-11-04')).toBe('2026-11-03T19:00:00.000Z');
  });

  it('ends the day at 23:59:59.999 Karachi, inclusive', () => {
    expect(karachiEndOfDayIso('2026-11-04')).toBe('2026-11-04T18:59:59.999Z');
  });

  it('a day window contains exactly the instants shown on that Karachi date', () => {
    const start = Date.parse(karachiStartOfDayIso('2026-11-04'));
    const end = Date.parse(karachiEndOfDayIso('2026-11-04'));
    const justBefore = new Date(start - 1);
    const firstMoment = new Date(start);
    const lastMoment = new Date(end);
    const nextDay = new Date(end + 1);
    expect(formatKarachiDate(justBefore)).toBe('03-Nov-2026');
    expect(formatKarachiDate(firstMoment)).toBe('04-Nov-2026');
    expect(formatKarachiDate(lastMoment)).toBe('04-Nov-2026');
    expect(formatKarachiDate(nextDay)).toBe('05-Nov-2026');
  });
});
