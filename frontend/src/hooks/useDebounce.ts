import { useEffect, useState } from 'react';

// Returns the value only after it stopped changing for delayMs. Used for the
// filters, so typing "facebook" sends one request, not eight.
export function useDebounce<T>(value: T, delayMs: number): T {
  const [debounced, setDebounced] = useState(value);

  useEffect(() => {
    const timer = setTimeout(() => setDebounced(value), delayMs);
    return () => clearTimeout(timer);
  }, [value, delayMs]);

  return debounced;
}
