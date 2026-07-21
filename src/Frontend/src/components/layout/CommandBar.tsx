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
import { useJobStore } from '../../store/jobStore';

function isTauriEnv(): boolean {
  return typeof window !== 'undefined' && '__TAURI_INTERNALS__' in window;
}

function getRuntimeLabel(): string {
  if (!isTauriEnv()) {
    return 'Web browser';
  }

  const origin = typeof window !== 'undefined' ? window.location.origin : '';
  if (origin.includes('localhost:5173') || origin.includes('localhost:1420')) {
    return 'Desktop source';
  }

  return 'Desktop packaged';
}

function formatBuildStamp(value: string): string {
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? value : date.toLocaleString();
}

const useStyles = makeStyles({
  toolbar: {
    borderBottom: `1px solid ${tokens.colorNeutralStroke2}`,
    padding: '10px 14px',
    display: 'flex',
    alignItems: 'flex-start',
    gap: '10px',
    backgroundColor: '#fafafa',
  },
  titleWrap: {
    display: 'flex',
    flexDirection: 'column',
    gap: '6px',
    minWidth: 0,
  },
  title: {
    fontWeight: tokens.fontWeightBold,
    fontSize: '18px',
  },
  subtitle: {
    color: tokens.colorNeutralForeground3,
    fontSize: tokens.fontSizeBase200,
  },
  buildWrap: {
    display: 'flex',
    alignItems: 'center',
    gap: '6px',
    flexWrap: 'wrap',
  },
  spacer: {
    flex: 1,
  },
  statusWrap: {
    display: 'flex',
    alignItems: 'center',
    gap: '8px',
    flexWrap: 'wrap',
    justifyContent: 'flex-end',
  },
});

export function CommandBar() {
  const styles = useStyles();
  const [backendState, setBackendState] = useState<'checking' | 'connected' | 'disconnected'>('checking');
  const [queueState, setQueueState] = useState<'unknown' | 'available' | 'unavailable' | 'bootstrapping'>('unknown');
  const [desktopVersion, setDesktopVersion] = useState('n/a');
  const jobId = useJobStore((s) => s.jobId);
  const jobStatus = useJobStore((s) => s.status);
  const jobProgress = useJobStore((s) => s.progress);
  const streamStatus = useJobStore((s) => s.streamStatus);
  const processingMode = useJobStore((s) => s.processingMode);
  const runtimeLabel = getRuntimeLabel();
  const buildStamp = formatBuildStamp(__APP_BUILD_STAMP__);

  const hasActiveJob = !!jobId && jobStatus !== 'idle';
  const jobStatusLabel = hasActiveJob
    ? `${jobStatus} ${Math.round(jobProgress)}%`
    : 'No active job';
  const jobBadgeColor =
    jobStatus === 'completed' ? 'success' :
    jobStatus === 'failed' ? 'danger' :
    'informative';
  const streamBadgeColor =
    streamStatus === 'connected' ? 'success' :
    streamStatus === 'disconnected' ? 'danger' : 'informative';
  const modeLabel =
    processingMode === 'direct' ? 'Direct mode' :
    processingMode === 'queue' ? 'Queue mode' : 'Mode pending';

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

  useEffect(() => {
    if (!isTauriEnv()) {
      return;
    }

    let cancelled = false;

    const loadVersion = async () => {
      try {
        const { invoke } = await import('@tauri-apps/api/core');
        const version = await invoke<string>('get_app_version');
        if (!cancelled) {
          setDesktopVersion(version);
        }
      } catch {
        if (!cancelled) {
          setDesktopVersion('unavailable');
        }
      }
    };

    loadVersion();

    return () => {
      cancelled = true;
    };
  }, []);

  return (
    <div className={styles.toolbar}>
      <VideoClipRegular fontSize={22} />
      <div className={styles.titleWrap}>
        <Text className={styles.title}>Video Subtitle Translator</Text>
        <div className={styles.buildWrap}>
          <Badge appearance="filled" color="informative">{runtimeLabel}</Badge>
          <Badge appearance="tint">UI v{__APP_UI_VERSION__}</Badge>
          <Badge appearance="tint">Desktop app {desktopVersion}</Badge>
          <Badge appearance="tint">Build {buildStamp}</Badge>
        </div>
        <Text className={styles.subtitle}>Live alignment: runtime, UI build, desktop app version, and backend mode are shown here.</Text>
      </div>
      <div className={styles.spacer} />
      <div className={styles.statusWrap}>
        {backendState === 'checking' && <Badge color="informative">Backend checking</Badge>}
        {backendState === 'connected' && <Badge color="success">Backend connected</Badge>}
        {backendState === 'disconnected' && <Badge color="danger">Backend disconnected</Badge>}

        {queueState === 'available' && <Badge color="success">Queue available</Badge>}
        {queueState === 'bootstrapping' && <Badge color="informative">Queue bootstrapping...</Badge>}
        {queueState === 'unavailable' && <Badge color="warning">Queue unavailable</Badge>}
        {queueState === 'unknown' && <Badge color="informative">Queue unknown</Badge>}
        {queueState === 'unavailable' && <Badge appearance="filled" color="warning">Direct mode active</Badge>}

        <Badge appearance="filled" color={jobBadgeColor}>{jobStatusLabel}</Badge>
        {hasActiveJob && <Badge appearance="tint">{modeLabel}</Badge>}
        {hasActiveJob && <Badge appearance="tint" color={streamBadgeColor}>Stream {streamStatus}</Badge>}
      </div>
    </div>
  );
}
