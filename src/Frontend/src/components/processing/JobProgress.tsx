import {
  makeStyles,
  Text,
  Badge,
} from '@fluentui/react-components';
import { useEffect, useMemo, useState } from 'react';
import { useJobStore } from '../../store/jobStore';
import { useSSE } from '../../hooks/useSSE';

const useStyles = makeStyles({
  section: {
    border: '1px solid rgba(15, 23, 42, 0.08)',
    borderRadius: '24px',
    padding: '20px',
    background: 'linear-gradient(145deg, #f8fbff 0%, #eef4ff 55%, #f7fbf8 100%)',
    boxShadow: '0 20px 60px rgba(15, 23, 42, 0.08)',
    marginTop: '14px',
  },
  content: {
    display: 'flex',
    flexDirection: 'column',
    gap: '14px',
  },
  sectionTitle: {
    fontWeight: 600,
    marginBottom: '2px',
  },
  statusRow: {
    display: 'flex',
    alignItems: 'center',
    gap: '10px',
    flexWrap: 'wrap',
  },
  heroRow: {
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'flex-start',
    gap: '12px',
    flexWrap: 'wrap',
  },
  stageBlock: {
    display: 'flex',
    flexDirection: 'column',
    gap: '6px',
    flex: '1 1 320px',
    minWidth: 0,
  },
  eyebrow: {
    letterSpacing: '0.08em',
    textTransform: 'uppercase',
    color: '#4f6b95',
  },
  stageHeadline: {
    fontSize: '20px',
    lineHeight: '28px',
    fontWeight: 700,
    color: '#0f172a',
  },
  progressPill: {
    alignSelf: 'flex-start',
    borderRadius: '999px',
    padding: '10px 16px',
    background: 'linear-gradient(135deg, #0f6cbd 0%, #2a7de1 45%, #38bdf8 100%)',
    color: '#fff',
    fontSize: '24px',
    lineHeight: '28px',
    fontWeight: 800,
    boxShadow: '0 12px 32px rgba(15, 108, 189, 0.28)',
  },
  progressTrack: {
    position: 'relative',
    width: '100%',
    height: '24px',
    borderRadius: '999px',
    overflow: 'hidden',
    background: 'rgba(15, 23, 42, 0.08)',
    boxShadow: 'inset 0 1px 2px rgba(15, 23, 42, 0.08)',
  },
  progressFill: {
    height: '100%',
    borderRadius: '999px',
    background: 'linear-gradient(90deg, #0f6cbd 0%, #2a7de1 45%, #38bdf8 100%)',
    boxShadow: '0 8px 24px rgba(42, 125, 225, 0.35)',
    transition: 'width 280ms ease',
  },
  metaRow: {
    display: 'flex',
    gap: '10px',
    flexWrap: 'wrap',
  },
  hintCard: {
    borderRadius: '18px',
    background: 'rgba(255, 255, 255, 0.78)',
    border: '1px solid rgba(15, 23, 42, 0.08)',
    padding: '12px 14px',
  },
  log: {
    marginTop: '4px',
    background: 'linear-gradient(180deg, #0f172a 0%, #162033 100%)',
    color: '#dbe6ff',
    borderRadius: '18px',
    fontFamily: 'Cascadia Code, Consolas, monospace',
    fontSize: '12px',
    padding: '14px',
    maxHeight: '220px',
    overflowY: 'auto',
    whiteSpace: 'pre-wrap',
    border: '1px solid rgba(148, 163, 184, 0.22)',
  },
});

export function JobProgress() {
  const styles = useStyles();
  const { jobId, status, progress, stage, processingMode, streamStatus } = useJobStore();
  const [history, setHistory] = useState<string[]>([]);
  const safeProgress = useMemo(
    () => (Number.isFinite(progress) ? Math.min(100, Math.max(0, progress)) : 0),
    [progress]
  );
  const elapsed = useMemo(() => `${Math.round(safeProgress)}%`, [safeProgress]);

  useSSE(jobId);

  useEffect(() => {
    if (!jobId) {
      setHistory([]);
      return;
    }

    if (!stage) return;
    setHistory((prev) => {
      const line = `[${new Date().toLocaleTimeString()}] ${stage}`;
      if (prev[prev.length - 1] === line) return prev;
      const next = [...prev, line];
      return next.slice(-25);
    });
  }, [jobId, stage]);

  if (!jobId || status === 'idle') return null;

  const badgeColor = status === 'completed' ? 'success' :
    status === 'failed' ? 'danger' : 'informative';

  const modeLabel = processingMode === 'direct'
    ? 'Direct mode (no queue)'
    : processingMode === 'queue'
      ? 'Queue mode'
      : 'Mode pending';

  const streamLabel = streamStatus === 'connected'
    ? 'Event stream connected'
    : streamStatus === 'connecting'
      ? 'Connecting event stream...'
      : streamStatus === 'disconnected'
        ? 'Event stream disconnected'
        : 'Event stream idle';

  const streamColor = streamStatus === 'connected'
    ? 'success'
    : streamStatus === 'connecting'
      ? 'informative'
      : streamStatus === 'disconnected'
        ? 'danger'
        : 'informative';

  const userHint = streamStatus === 'disconnected'
    ? 'Progress updates are interrupted. Keep this page open and retry start if the status does not change.'
    : status === 'queued'
      ? 'Job is in queue: waiting for a free worker slot. As soon as processing starts, stage logs and progress will update live.'
    : processingMode === 'direct'
      ? 'Queue is unavailable: processing runs directly in API mode. This is slower but automatic.'
      : processingMode === 'queue'
      ? 'Queue mode active: job is processed asynchronously by worker services, or by the local in-process queue fallback when external infrastructure is unavailable.'
        : 'Preparing processing context...';

  return (
    <div className={styles.section}>
      <Text className={styles.sectionTitle}>Progress</Text>
      <div className={styles.content}>
        <div className={styles.statusRow}>
          <Badge color={badgeColor} appearance="filled">
            {status}
          </Badge>
          <Badge appearance="tint">{modeLabel}</Badge>
          <Badge color={streamColor} appearance="tint">{streamLabel}</Badge>
        </div>
        <div className={styles.heroRow}>
          <div className={styles.stageBlock}>
            <Text size={200} className={styles.eyebrow}>Current stage</Text>
            <Text className={styles.stageHeadline}>▶ {stage || 'Ready'}</Text>
          </div>
          <div className={styles.progressPill}>{elapsed}</div>
        </div>
        <div className={styles.progressTrack} aria-label={`Processing progress ${elapsed}`} role="progressbar" aria-valuenow={Math.round(safeProgress)} aria-valuemin={0} aria-valuemax={100}>
          <div className={styles.progressFill} style={{ width: `${safeProgress}%` }} />
        </div>
        <div className={styles.metaRow}>
          <Badge appearance="filled" color="brand">Absolute pipeline progress</Badge>
          <Badge appearance="tint">Live stream + snapshot fallback</Badge>
        </div>
        <div className={styles.hintCard}>
          <Text size={300}>{userHint}</Text>
        </div>
        <Text className={styles.sectionTitle}>Log</Text>
        <div className={styles.log}>
          {history.length === 0 ? 'Waiting for progress events...' : history.join('\n')}
        </div>
      </div>
    </div>
  );
}
