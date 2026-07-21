import { create } from 'zustand';

export type JobStatus =
  | 'idle'
  | 'uploading'
  | 'queued'
  | 'extracting-audio'
  | 'transcribing'
  | 'translating'
  | 'burning-subtitles'
  | 'generating-subtitles'
  | 'completed'
  | 'failed';

export type ProcessingMode = 'queue' | 'direct' | 'unknown';
export type StreamStatus = 'idle' | 'connecting' | 'connected' | 'disconnected';

interface JobState {
  jobId: string | null;
  status: JobStatus;
  progress: number;
  stage: string;
  processingMode: ProcessingMode;
  streamStatus: StreamStatus;
  setJob: (jobId: string, mode?: ProcessingMode, stage?: string) => void;
  updateProgress: (status: JobStatus, progress: number, stage: string) => void;
  setStreamStatus: (streamStatus: StreamStatus) => void;
  reset: () => void;
}

export const useJobStore = create<JobState>((set) => ({
  jobId: null,
  status: 'idle',
  progress: 0,
  stage: '',
  processingMode: 'unknown',
  streamStatus: 'idle',
  setJob: (jobId, mode = 'unknown', stage = 'Job accepted. Waiting for processing updates...') =>
    set({ jobId, status: 'queued', progress: 0, stage, processingMode: mode, streamStatus: 'connecting' }),
  updateProgress: (status, progress, stage) => set({ status, progress, stage }),
  setStreamStatus: (streamStatus) => set({ streamStatus }),
  reset: () => set({
    jobId: null,
    status: 'idle',
    progress: 0,
    stage: '',
    processingMode: 'unknown',
    streamStatus: 'idle',
  }),
}));
