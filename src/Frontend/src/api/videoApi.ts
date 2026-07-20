import httpClient from './httpClient';

export interface UploadResponse {
  jobId: string;
  status: string;
  detail?: string;
}

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

export async function uploadVideo(
  file: File,
  options: {
    sourceLanguage?: string;
    targetLanguages: string;
    modelSize: string;
    burnSubtitles: boolean;
  }
): Promise<UploadResponse> {
  const formData = new FormData();
  formData.append('file', file);
  if (options.sourceLanguage) formData.append('sourceLanguage', options.sourceLanguage);
  formData.append('targetLanguages', options.targetLanguages);
  formData.append('modelSize', options.modelSize);
  formData.append('burnSubtitles', String(options.burnSubtitles));

  const response = await httpClient.post<UploadResponse>('/video/upload', formData);
  return response.data;
}

export async function processLocalVideo(
  videoPath: string,
  options: {
    sourceLanguage?: string;
    targetLanguages: string;
    modelSize: string;
    burnSubtitles: boolean;
  }
): Promise<UploadResponse> {
  const response = await httpClient.post<UploadResponse>('/video/process-local', {
    videoPath,
    sourceLanguage: options.sourceLanguage,
    targetLanguages: options.targetLanguages,
    modelSize: options.modelSize,
    burnSubtitles: options.burnSubtitles,
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

export function getVideoStreamUrl(jobId: string): string {
  return `/api/player/stream/${jobId}`;
}
