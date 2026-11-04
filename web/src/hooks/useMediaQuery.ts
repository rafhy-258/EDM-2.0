import { useEffect, useState } from 'react';

/** Tracks a CSS media query. Returns false when matchMedia is unavailable (for example in some test environments). */
export function useMediaQuery(query: string): boolean {
  const [matches, setMatches] = useState<boolean>(() => readMatch(query));

  useEffect(() => {
    if (typeof window.matchMedia !== 'function') return;
    const mql = window.matchMedia(query);
    const onChange = () => setMatches(mql.matches);
    onChange();
    mql.addEventListener('change', onChange);
    return () => mql.removeEventListener('change', onChange);
  }, [query]);

  return matches;
}

function readMatch(query: string): boolean {
  return typeof window.matchMedia === 'function' ? window.matchMedia(query).matches : false;
}
