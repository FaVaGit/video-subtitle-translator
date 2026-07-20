import axios from 'axios';

const httpClient = axios.create({
  baseURL: '/api',
  timeout: 300000, // 5min for uploads
});

export interface HealthStatus {
  backend: 'ok';
  queue: 'available' | 'unavailable';
}

export async function getHealthStatus(): Promise<HealthStatus> {
  const response = await httpClient.get<HealthStatus>('/health', { timeout: 4000 });
  return response.data;
}

export default httpClient;
