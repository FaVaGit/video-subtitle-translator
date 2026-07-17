import {
  makeStyles,
  ProgressBar,
  Text,
  Badge,
} from '@fluentui/react-components';
import { useEffect, useMemo, useState } from 'react';
import { useJobStore } from '../../store/jobStore';
import { useSSE } from '../../hooks/useSSE';

const useStyles = makeStyles({
  section: {
    border: '1px solid #d0d0d0',
    borderRadius: '6px',
    padding: '10px',
    backgroundColor: '#fff',
    marginTop: '10px',
  },
  content: {
    display: 'flex',
    flexDirection: 'column',
    gap: '8px',
  },
  sectionTitle: {
    fontWeight: 600,
    marginBottom: '8px',
  },
  statusRow: {
    display: 'flex',
    alignItems: 'center',
    gap: '8px',
  },
  log: {
    marginTop: '8px',
    backgroundColor: '#1e1e1e',
    color: '#d4d4d4',
    borderRadius: '4px',
    fontFamily: 'Consolas, monospace',
    fontSize: '12px',
    padding: '8px',
    maxHeight: '180px',
    overflowY: 'auto',
    whiteSpace: 'pre-wrap',
  },
});

export function JobProgress() {
  const styles = useStyles();
  const { jobId, status, progress, stage } = useJobStore();
  const [history, setHistory] = useState<string[]>([]);
  const elapsed = useMemo(() => `${Math.max(0, Math.round(progress))}%`, [progress]);

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

  return (
    <div className={styles.section}>
      <Text className={styles.sectionTitle}>Progress</Text>
      <div className={styles.content}>
        <div className={styles.statusRow}>
          <Badge color={badgeColor} appearance="filled">
            {status}
          </Badge>
          <Text weight="semibold">▶ {stage || 'Ready'}</Text>
        </div>
        <ProgressBar value={progress / 100} />
        <Text size={200}>{elapsed} complete</Text>
        <Text className={styles.sectionTitle}>Log</Text>
        <div className={styles.log}>
          {history.length === 0 ? 'Waiting for progress events...' : history.join('\n')}
        </div>
      </div>
    </div>
  );
}
