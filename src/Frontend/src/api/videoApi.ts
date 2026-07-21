import httpClient from './httpClient';
import { getApiUrl } from './apiBase';

export interface UploadResponse {
  jobId: string;
  status: string;
  detail?: string;
}

export type RequestedProcessingMode = 'auto' | 'direct' | 'queue';

export interface SubtitleTrack {
  language: string;
  label: string;
  url: string;
}

export interface SubtitleCue {
  index: number;
  start: number;
  end: number;
  text: string;
}

export interface JobProgressSnapshot {
  jobId?: string;
  status?: string;
  progressPercent?: number;
  stage?: string;
}

export async function uploadVideo(
  file: File,
  options: {
    sourceLanguage?: string;
    targetLanguages: string;
    modelSize: string;
    burnSubtitles: boolean;
    processingMode: RequestedProcessingMode;
  },
  onUploadProgress?: (percent: number) => void
): Promise<UploadResponse> {
  const formData = new FormData();
  formData.append('file', file);
  if (options.sourceLanguage) formData.append('sourceLanguage', options.sourceLanguage);
  formData.append('targetLanguages', options.targetLanguages);
  formData.append('modelSize', options.modelSize);
  formData.append('burnSubtitles', String(options.burnSubtitles));
  formData.append('processingMode', options.processingMode);

  const response = await httpClient.post<UploadResponse>('/video/upload', formData, {
    onUploadProgress: (event) => {
      if (!onUploadProgress || !event.total) return;
      onUploadProgress(Math.round((event.loaded / event.total) * 100));
    },
  });
  return response.data;
}

export async function processLocalVideo(
  videoPath: string,
  options: {
    sourceLanguage?: string;
    targetLanguages: string;
    modelSize: string;
    burnSubtitles: boolean;
    processingMode: RequestedProcessingMode;
    overwriteOriginalSubtitle?: boolean;
  }
): Promise<UploadResponse> {
  const response = await httpClient.post<UploadResponse>('/video/process-local', {
    videoPath,
    sourceLanguage: options.sourceLanguage,
    targetLanguages: options.targetLanguages,
    modelSize: options.modelSize,
    burnSubtitles: options.burnSubtitles,
    processingMode: options.processingMode,
    overwriteOriginalSubtitle: !!options.overwriteOriginalSubtitle,
  });
  return response.data;
}

export async function getTracks(jobId: string): Promise<SubtitleTrack[]> {
  const response = await httpClient.get<SubtitleTrack[]>(`/player/${jobId}/tracks`);
  return response.data;
}

export async function getSubtitleCues(jobId: string, lang: string): Promise<SubtitleCue[]> {
  const response = await httpClient.get<SubtitleCue[]>(`/player/${jobId}/subtitles/${lang}?format=json`);
  return response.data;
}

export async function getLatestJobProgress(jobId: string): Promise<JobProgressSnapshot | null> {
  try {
    const response = await httpClient.get<JobProgressSnapshot>(`/jobs/${jobId}/latest-progress`, {
      timeout: 4000,
    });
    return response.data;
  } catch {
    return null;
  }
}

export function getVideoStreamUrl(jobId: string): string {
  return getApiUrl(`/player/stream/${jobId}`);
}

export async function cancelJob(jobId: string): Promise<UploadResponse> {
  const response = await httpClient.post<UploadResponse>(`/video/${jobId}/cancel`);
  return response.data;
}

export async function openOutputFolder(jobId: string): Promise<void> {
  await httpClient.post(`/video/${jobId}/open-output-folder`);
}
