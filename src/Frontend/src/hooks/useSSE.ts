import { useEffect, useRef } from 'react';
import { createSSEConnection } from '../api/sseClient';
import { useJobStore, JobStatus } from '../store/jobStore';

interface ProgressEvent {
  jobId: string;
  status: string;
  progressPercent: number;
  stage: string;
}

function mapStatus(status: string): JobStatus {
  const map: Record<string, JobStatus> = {
    Queued: 'queued',
    ExtractingAudio: 'extracting-audio',
    Transcribing: 'transcribing',
    Translating: 'translating',
    GeneratingSubtitles: 'generating-subtitles',
    Completed: 'completed',
    Failed: 'failed',
  };
  return map[status] ?? 'queued';
}

export function useSSE(jobId: string | null) {
  const closeRef = useRef<(() => void) | null>(null);
  const updateProgress = useJobStore((s) => s.updateProgress);

  useEffect(() => {
    if (!jobId) return;

    closeRef.current = createSSEConnection<ProgressEvent>(
      `/api/jobs/${jobId}/progress`,
      (data) => {
        updateProgress(mapStatus(data.status), data.progressPercent, data.stage);
      },
      () => {
        // Connection closed
      }
    );

    return () => {
      closeRef.current?.();
    };
  }, [jobId, updateProgress]);
}
