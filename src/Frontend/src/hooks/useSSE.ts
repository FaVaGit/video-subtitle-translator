import { useEffect, useRef } from 'react';
import { createSSEConnection } from '../api/sseClient';
import { getLatestJobProgress } from '../api/videoApi';
import { getApiUrl } from '../api/apiBase';
import { useJobStore, JobStatus } from '../store/jobStore';

interface ProgressEvent {
  jobId?: string;
  JobId?: string;
  status?: string | number;
  Status?: string | number;
  progressPercent?: number;
  ProgressPercent?: number;
  stage?: string;
  Stage?: string;
}

function mapStatus(status: string | number): JobStatus {
  const numericMap: Record<number, JobStatus> = {
    0: 'queued',
    1: 'extracting-audio',
    2: 'transcribing',
    3: 'translating',
    4: 'generating-subtitles',
    5: 'burning-subtitles',
    6: 'completed',
    7: 'failed',
  };

  if (typeof status === 'number') {
    return numericMap[status] ?? 'queued';
  }

  const map: Record<string, JobStatus> = {
    Queued: 'queued',
    ExtractingAudio: 'extracting-audio',
    Transcribing: 'transcribing',
    Translating: 'translating',
    BurningSubtitles: 'burning-subtitles',
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

  const applyProgress = (data: ProgressEvent) => {
    const rawStatus = data.status ?? data.Status;
    const rawStage = data.stage ?? data.Stage;
    const rawPercent = data.progressPercent ?? data.ProgressPercent;
    const numericPercent = typeof rawPercent === 'number' && Number.isFinite(rawPercent)
      ? Math.min(100, Math.max(0, rawPercent))
      : 0;

    updateProgress(
      mapStatus(rawStatus ?? 'Queued'),
      numericPercent,
      rawStage && rawStage.trim() ? rawStage : 'Waiting for worker updates...'
    );
  };

  useEffect(() => {
    if (!jobId) {
      setStreamStatus('idle');
      return;
    }

    setStreamStatus('connecting');

    closeRef.current = createSSEConnection<ProgressEvent>(
      getApiUrl(`/jobs/${jobId}/progress`),
      (data) => {
        setStreamStatus('connected');
        applyProgress(data);
      },
      () => {
        setStreamStatus('disconnected');
      }
    );

    const pollId = window.setInterval(async () => {
      const latest = await getLatestJobProgress(jobId);
      if (!latest) return;
      applyProgress(latest);
    }, 1500);

    return () => {
      closeRef.current?.();
      window.clearInterval(pollId);
      setStreamStatus('idle');
    };
  }, [jobId, setStreamStatus, updateProgress]);
}
