import { setCurrentUser } from './api';

// Persisted login so the session survives reloads and is shared across the
// console and every standalone phase root (which are separate React apps).
export type Session = { userId: string; name: string };

const KEY = 'session';

export function getSession(): Session | null {
  try {
    const raw = localStorage.getItem(KEY);
    return raw ? (JSON.parse(raw) as Session) : null;
  } catch {
    return null;
  }
}

export function saveSession(s: Session) {
  localStorage.setItem(KEY, JSON.stringify(s));
  setCurrentUser(s.userId);
}

export function clearSession() {
  localStorage.removeItem(KEY);
  setCurrentUser('');
}

// Re-arm the api X-User-Id header from a stored session (used on every root at
// startup, including phase roots that never run the login form).
export function restoreSession(): Session | null {
  const s = getSession();
  if (s) setCurrentUser(s.userId);
  return s;
}
