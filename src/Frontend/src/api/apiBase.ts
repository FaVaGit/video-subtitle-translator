function isTauriEnv(): boolean {
  return typeof window !== 'undefined' && '__TAURI_INTERNALS__' in window;
}

export function getApiBaseUrl(): string {
  const envBase = import.meta.env.VITE_API_BASE_URL as string | undefined;
  if (envBase && envBase.trim()) {
    return envBase.trim().replace(/\/$/, '');
  }

  if (isTauriEnv()) {
    return 'http://localhost:5000/api';
  }

  return '/api';
}

export function getApiUrl(path: string): string {
  const normalizedPath = path.startsWith('/') ? path : `/${path}`;
  const base = getApiBaseUrl();

  if (base === '/api') {
    return `/api${normalizedPath}`;
  }

  return `${base}${normalizedPath}`;
}
