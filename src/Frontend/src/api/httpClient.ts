import axios from 'axios';

const httpClient = axios.create({
  baseURL: '/api',
  timeout: 300000, // 5min for uploads
});

export default httpClient;
