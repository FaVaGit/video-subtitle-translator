import {
  makeStyles,
  Card,
  CardHeader,
  ProgressBar,
  Text,
  Badge,
} from '@fluentui/react-components';
import { useJobStore } from '../../store/jobStore';
import { useSSE } from '../../hooks/useSSE';

const useStyles = makeStyles({
  card: {
    maxWidth: '600px',
    margin: '16px auto',
  },
  content: {
    padding: '16px',
    display: 'flex',
    flexDirection: 'column',
    gap: '12px',
  },
  statusRow: {
    display: 'flex',
    alignItems: 'center',
    gap: '8px',
  },
});

export function JobProgress() {
  const styles = useStyles();
  const { jobId, status, progress, stage } = useJobStore();

  useSSE(jobId);

  if (!jobId || status === 'idle') return null;

  const badgeColor = status === 'completed' ? 'success' :
    status === 'failed' ? 'danger' : 'informative';

  return (
    <Card className={styles.card}>
      <CardHeader header={<Text weight="semibold">Processing Status</Text>} />
      <div className={styles.content}>
        <div className={styles.statusRow}>
          <Badge color={badgeColor} appearance="filled">
            {status}
          </Badge>
          <Text size={200}>{stage}</Text>
        </div>
        <ProgressBar value={progress / 100} />
        <Text size={200}>{progress}% complete</Text>
      </div>
    </Card>
  );
}
