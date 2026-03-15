interface ClientConfig {
  baseUrl?: string;
  headers?: Record<string, string>;
}

interface RequestConfig<TBody> {
  path: string;
  method?: 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE';
  body?: TBody;
  signal?: AbortSignal;
}

const defaultHeaders = {
  'Content-Type': 'application/json'
};

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? '/api';

export function createApiClient(config: ClientConfig = {}) {
  const baseUrl = config.baseUrl ?? API_BASE_URL;
  const headers = { ...defaultHeaders, ...config.headers };

  return async function apiClient<TResponse, TBody = unknown>(request: RequestConfig<TBody>) {
    const response = await fetch(baseUrl + request.path, {
      method: request.method ?? 'GET',
      headers,
      body: request.body ? JSON.stringify(request.body) : undefined,
      signal: request.signal
    });

    if (!response.ok) {
      throw new Error('Request failed with status ' + response.status);
    }

    if (response.status === 204) {
      return null as TResponse;
    }

    return (await response.json()) as TResponse;
  };
}

export const apiClient = createApiClient();
