import { create } from 'zustand';

export type JobStatus =
  | 'idle'
  | 'uploading'
  | 'queued'
  | 'extracting-audio'
  | 'transcribing'
  | 'translating'
  | 'generating-subtitles'
  | 'completed'
  | 'failed';

interface JobState {
  jobId: string | null;
  status: JobStatus;
  progress: number;
  stage: string;
  setJob: (jobId: string) => void;
  updateProgress: (status: JobStatus, progress: number, stage: string) => void;
  reset: () => void;
}

export const useJobStore = create<JobState>((set) => ({
  jobId: null,
  status: 'idle',
  progress: 0,
  stage: '',
  setJob: (jobId) => set({ jobId, status: 'queued', progress: 0 }),
  updateProgress: (status, progress, stage) => set({ status, progress, stage }),
  reset: () => set({ jobId: null, status: 'idle', progress: 0, stage: '' }),
}));
