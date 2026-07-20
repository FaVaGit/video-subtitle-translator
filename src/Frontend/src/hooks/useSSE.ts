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
  const setStreamStatus = useJobStore((s) => s.setStreamStatus);

  useEffect(() => {
    if (!jobId) {
      setStreamStatus('idle');
      return;
    }

    setStreamStatus('connecting');

    closeRef.current = createSSEConnection<ProgressEvent>(
      `/api/jobs/${jobId}/progress`,
      (data) => {
        setStreamStatus('connected');
        updateProgress(mapStatus(data.status), data.progressPercent, data.stage);
      },
      () => {
        setStreamStatus('disconnected');
      }
    );

    return () => {
      closeRef.current?.();
      setStreamStatus('idle');
    };
  }, [jobId, setStreamStatus, updateProgress]);
}
