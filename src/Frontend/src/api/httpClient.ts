import axios from 'axios';
import { getApiBaseUrl } from './apiBase';

const httpClient = axios.create({
  baseURL: getApiBaseUrl(),
  timeout: 300000, // 5min for uploads
});

export interface HealthStatus {
  backend: 'ok';
  queue: 'available' | 'unavailable' | 'bootstrapping';
}

export async function getHealthStatus(): Promise<HealthStatus> {
  const response = await httpClient.get<HealthStatus>('/health', { timeout: 4000 });
  return response.data;
}

export default httpClient;
