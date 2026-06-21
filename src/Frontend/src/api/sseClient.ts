export interface SSEEvent<T> {
  data: T;
}

export function createSSEConnection<T>(
  url: string,
  onMessage: (data: T) => void,
  onError?: (error: Event) => void
): () => void {
  const source = new EventSource(url);

  source.onmessage = (event) => {
    try {
      const data = JSON.parse(event.data) as T;
      onMessage(data);
    } catch {
      // Skip malformed messages
    }
  };

  source.onerror = (event) => {
    onError?.(event);
    source.close();
  };

  return () => source.close();
}
