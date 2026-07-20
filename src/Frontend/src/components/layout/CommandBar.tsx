import { useEffect, useState } from 'react';
import {
  makeStyles,
  tokens,
  Text,
  Badge,
} from '@fluentui/react-components';
import {
  VideoClipRegular,
} from '@fluentui/react-icons';
import { getHealthStatus } from '../../api/httpClient';

const useStyles = makeStyles({
  toolbar: {
    borderBottom: `1px solid ${tokens.colorNeutralStroke2}`,
    padding: '10px 14px',
    display: 'flex',
    alignItems: 'center',
    gap: '10px',
    backgroundColor: '#fafafa',
  },
  title: {
    fontWeight: tokens.fontWeightBold,
    fontSize: '18px',
  },
  subtitle: {
    color: tokens.colorNeutralForeground3,
    fontSize: tokens.fontSizeBase200,
  },
  spacer: {
    flex: 1,
  },
  statusWrap: {
    display: 'flex',
    alignItems: 'center',
    gap: '8px',
    flexWrap: 'wrap',
  },
});

export function CommandBar() {
  const styles = useStyles();
  const [backendState, setBackendState] = useState<'checking' | 'connected' | 'disconnected'>('checking');
  const [queueState, setQueueState] = useState<'unknown' | 'available' | 'unavailable'>('unknown');

  useEffect(() => {
    let cancelled = false;

    const check = async () => {
      try {
        const health = await getHealthStatus();
        if (cancelled) return;
        setBackendState('connected');
        setQueueState(health.queue);
      } catch {
        if (cancelled) return;
        setBackendState('disconnected');
        setQueueState('unknown');
      }
    };

    check();
    const id = window.setInterval(check, 7000);

    return () => {
      cancelled = true;
      window.clearInterval(id);
    };
  }, []);

  return (
    <div className={styles.toolbar}>
      <VideoClipRegular fontSize={22} />
      <div>
        <Text className={styles.title}>Video Subtitle Translator</Text>
        <Text className={styles.subtitle}>Desktop-style workflow: files, settings, progress, log</Text>
      </div>
      <div className={styles.spacer} />
      <div className={styles.statusWrap}>
        {backendState === 'checking' && <Badge color="informative">Backend checking</Badge>}
        {backendState === 'connected' && <Badge color="success">Backend connected</Badge>}
        {backendState === 'disconnected' && <Badge color="danger">Backend disconnected</Badge>}

        {queueState === 'available' && <Badge color="success">Queue available</Badge>}
        {queueState === 'unavailable' && <Badge color="warning">Queue unavailable</Badge>}
        {queueState === 'unknown' && <Badge color="informative">Queue unknown</Badge>}
      </div>
    </div>
  );
}
