import { useState, useCallback, useRef } from 'react';
import axios from 'axios';
import {
  makeStyles,
  tokens,
  Button,
  Input,
  Select,
  Checkbox,
  Label,
  Text,
} from '@fluentui/react-components';
import { ArrowUploadRegular } from '@fluentui/react-icons';
import { cancelJob, openOutputFolder, processLocalVideo, RequestedProcessingMode, uploadVideo } from '../../api/videoApi';
import { getHealthStatus } from '../../api/httpClient';
import { useJobStore } from '../../store/jobStore';

function isTauriEnv(): boolean {
  return typeof window !== 'undefined' && '__TAURI_INTERNALS__' in window;
}

function basename(path: string): string {
  const parts = path.split(/[\\/]/);
  return parts[parts.length - 1] || path;
}

function normalizeLocalVideoPath(value: string): string {
  let path = value.trim().replace(/^"|"$/g, '');
  if (!path) return '';

  if (path.startsWith('file://')) {
    try {
      const url = new URL(path);
      if (url.protocol === 'file:') {
        path = decodeURIComponent(url.pathname || '');
      }
    } catch {
      // Keep original value if parsing fails.
    }
  }

  // Convert /C:/foo (URI-style Windows path) to C:\foo.
  if (/^\/[A-Za-z]:\//.test(path)) {
    path = path.slice(1);
  }

  if (/^[A-Za-z]:\//.test(path)) {
    path = path.replace(/\//g, '\\\\');
  }

  return path;
}

const useStyles = makeStyles({
  root: {
    display: 'flex',
    flexDirection: 'column',
    gap: '10px',
  },
  section: {
    border: `1px solid ${tokens.colorNeutralStroke2}`,
    borderRadius: tokens.borderRadiusMedium,
    padding: '10px',
    backgroundColor: '#ffffff',
  },
  sectionTitle: {
    fontWeight: tokens.fontWeightSemibold,
    marginBottom: '8px',
  },
  row: {
    display: 'grid',
    gridTemplateColumns: '130px 1fr auto',
    alignItems: 'center',
    gap: '8px',
    marginBottom: '8px',
  },
  rowNoButton: {
    display: 'grid',
    gridTemplateColumns: '130px 1fr',
    alignItems: 'center',
    gap: '8px',
    marginBottom: '8px',
  },
  dropzone: {
    border: `2px dashed ${tokens.colorNeutralStroke1}`,
    borderRadius: tokens.borderRadiusMedium,
    padding: '18px',
    textAlign: 'center' as const,
    cursor: 'pointer',
    position: 'relative',
    ':hover': {
      borderTopColor: tokens.colorBrandStroke1,
      borderRightColor: tokens.colorBrandStroke1,
      borderBottomColor: tokens.colorBrandStroke1,
      borderLeftColor: tokens.colorBrandStroke1,
      backgroundColor: tokens.colorNeutralBackground1Hover,
    },
  },
  tiny: {
    fontSize: tokens.fontSizeBase200,
    color: tokens.colorNeutralForeground3,
  },
  languagesGrid: {
    display: 'grid',
    gridTemplateColumns: 'repeat(auto-fit, minmax(160px, 1fr))',
    gap: '6px 10px',
  },
  actions: {
    display: 'flex',
    alignItems: 'center',
    gap: '8px',
    flexWrap: 'wrap',
  },
  hwStrong: {
    fontWeight: tokens.fontWeightSemibold,
  },
  errorText: {
    color: tokens.colorPaletteRedForeground1,
  },
});

async function getUploadErrorMessage(error: unknown): Promise<string> {
  if (axios.isAxiosError(error)) {
    if (!error.response || error.code === 'ERR_NETWORK') {
      try {
        await getHealthStatus();
        return 'Connection to upload endpoint failed. The backend is reachable, but the request was interrupted. Retry Start Processing.';
      } catch {
        return 'Cannot reach backend API. Verify backend is running (Backend connected badge) and retry.';
      }
    }

    const data = error.response?.data;
    if (data && typeof data === 'object') {
      const detail = (data as { detail?: unknown }).detail;
      const message = (data as { error?: unknown }).error;
      if (typeof detail === 'string' && typeof message === 'string') {
        return `${message} ${detail}`;
      }
      if (typeof detail === 'string') return detail;
      if (typeof message === 'string') return message;
    }
    if (typeof data === 'string' && data.trim()) return data;
    if (error.message) return error.message;
  }

  return 'Upload failed. Verify backend status, then retry Start Processing.';
}

function isRetryableUploadError(error: unknown): boolean {
  if (!axios.isAxiosError(error)) return false;
  if (error.response) return false;

  const code = error.code ?? '';
  return code === 'ERR_NETWORK' || code === 'ECONNABORTED' || code === 'ECONNRESET';
}

const MODELS = ['tiny', 'base', 'small', 'medium', 'large-v3'];
const SOURCES = [
  'auto', 'en', 'it', 'fr', 'de', 'es', 'pt', 'zh', 'ja', 'ko', 'ar', 'ru', 'hi',
  'nl', 'pl', 'tr', 'sv', 'da', 'no', 'fi', 'el', 'ro', 'hu', 'cs', 'uk',
];
const LANGS = [
  { code: 'en', name: 'English' },
  { code: 'it', name: 'Italiano' },
  { code: 'fr', name: 'Français' },
  { code: 'de', name: 'Deutsch' },
  { code: 'es', name: 'Español' },
  { code: 'pt', name: 'Português' },
  { code: 'zh', name: '中文' },
  { code: 'ja', name: '日本語' },
  { code: 'ko', name: '한국어' },
  { code: 'ar', name: 'العربية' },
  { code: 'ru', name: 'Русский' },
  { code: 'hi', name: 'हिन्दी' },
  { code: 'nl', name: 'Nederlands' },
  { code: 'pl', name: 'Polski' },
  { code: 'tr', name: 'Türkçe' },
  { code: 'sv', name: 'Svenska' },
  { code: 'da', name: 'Dansk' },
  { code: 'no', name: 'Norsk' },
  { code: 'fi', name: 'Suomi' },
  { code: 'el', name: 'Ελληνικά' },
  { code: 'ro', name: 'Română' },
  { code: 'hu', name: 'Magyar' },
  { code: 'cs', name: 'Čeština' },
  { code: 'uk', name: 'Українська' },
];

export function VideoUploader() {
  const styles = useStyles();
  const [file, setFile] = useState<File | null>(null);
  const [sourceLanguage, setSourceLanguage] = useState('auto');
  const [targetLangs, setTargetLangs] = useState<string[]>(['it', 'en']);
  const [modelSize, setModelSize] = useState('medium');
  const [burnSubs, setBurnSubs] = useState(false);
  const [processingMode, setProcessingMode] = useState<RequestedProcessingMode>('auto');
  const [uploading, setUploading] = useState(false);
  const [uploadPercent, setUploadPercent] = useState(0);
  const [uploadError, setUploadError] = useState('');
  const [localPath, setLocalPath] = useState('');
  const [nativeFileName, setNativeFileName] = useState('');
  const [actionBusy, setActionBusy] = useState<'cancel' | 'open-folder' | null>(null);
  const fileInputRef = useRef<HTMLInputElement | null>(null);
  const setJob = useJobStore((s) => s.setJob);
  const updateProgress = useJobStore((s) => s.updateProgress);
  const activeJobId = useJobStore((s) => s.jobId);
  const jobStatus = useJobStore((s) => s.status);
  const currentProgress = useJobStore((s) => s.progress);

  const handleFileChange = useCallback((e: React.ChangeEvent<HTMLInputElement>) => {
    const selected = e.target.files?.[0];
    if (selected) {
      setFile(selected);
      setNativeFileName('');
      setUploadError('');
      // Some non-standard webviews (e.g. Electron-like environments) expose a real
      // filesystem path on the File object. When available, use it directly.
      const maybePath = (selected as unknown as { path?: string }).path;
      setLocalPath(maybePath ?? '');
    }
  }, []);

  const handleSubmit = async () => {
    if (!file) return;
    if (targetLangs.length === 0) return;
    setUploading(true);
    setUploadPercent(0);
    setUploadError('');
    try {
      const uploadRequest = () => uploadVideo(file, {
        sourceLanguage: sourceLanguage === 'auto' ? undefined : sourceLanguage,
        targetLanguages: targetLangs.join(','),
        modelSize,
        burnSubtitles: burnSubs,
        processingMode,
      }, setUploadPercent);

      let result;
      try {
        result = await uploadRequest();
      } catch (firstError) {
        if (isRetryableUploadError(firstError)) {
          try {
            await getHealthStatus();
            result = await uploadRequest();
          } catch (retryError) {
            setUploadError(await getUploadErrorMessage(retryError));
            return;
          }
        } else {
          throw firstError;
        }
      }

      const mode = result.status === 'processing-direct' ? 'direct' : result.status === 'queued' ? 'queue' : 'unknown';
      const initialStage = result.detail ??
        (mode === 'direct'
          ? 'Queue unavailable: direct processing started in API mode.'
          : 'Job queued. Waiting for worker progress...');
      setJob(result.jobId, mode, initialStage);
      updateProgress('queued', 0, initialStage);
    } catch (error) {
      setUploadError(await getUploadErrorMessage(error));
    } finally {
      setUploading(false);
      setUploadPercent(0);
    }
  };

  const handleSubmitLocalPath = async () => {
    const path = normalizeLocalVideoPath(localPath);
    if (!path) return;
    if (targetLangs.length === 0) return;

    setUploading(true);
    setUploadError('');
    try {
      const submitLocal = async (overwriteOriginalSubtitle: boolean) => processLocalVideo(path, {
        sourceLanguage: sourceLanguage === 'auto' ? undefined : sourceLanguage,
        targetLanguages: targetLangs.join(','),
        modelSize,
        burnSubtitles: burnSubs,
        processingMode,
        overwriteOriginalSubtitle,
      });

      let result;
      try {
        result = await submitLocal(false);
      } catch (firstError) {
        if (axios.isAxiosError(firstError) && firstError.response?.status === 409) {
          const data = firstError.response.data as { code?: unknown; detail?: unknown } | undefined;
          if (data?.code === 'all_translations_exist') {
            const detail = typeof data.detail === 'string'
              ? data.detail
              : 'All translated subtitle files already exist.';
            const overwrite = window.confirm(`${detail}\n\nDo you want to overwrite the original subtitle and regenerate outputs?`);
            if (!overwrite) {
              setUploadError('Operation cancelled: existing translated subtitle files were kept.');
              return;
            }

            result = await submitLocal(true);
          } else {
            throw firstError;
          }
        } else {
          throw firstError;
        }
      }

      const mode = result.status === 'processing-direct' ? 'direct' : result.status === 'queued' ? 'queue' : 'unknown';
      const initialStage = result.detail ??
        (mode === 'direct'
          ? 'Local path accepted: direct processing started in API mode.'
          : 'Local path accepted. Job queued. Waiting for worker progress...');
      setJob(result.jobId, mode, initialStage);
      updateProgress('queued', 0, initialStage);
    } catch (error) {
      setUploadError(await getUploadErrorMessage(error));
    } finally {
      setUploading(false);
    }
  };

  const toggleLang = (code: string, checked: boolean) => {
    setTargetLangs((prev) => {
      if (checked) {
        if (prev.includes(code)) return prev;
        return [...prev, code];
      }
      return prev.filter((x) => x !== code);
    });
  };

  const handlePickVideo = async () => {
    if (isTauriEnv()) {
      try {
        const { invoke } = await import('@tauri-apps/api/core');
        const path = await invoke<string | null>('open_file_dialog');
        if (path) {
          setFile(null);
          setLocalPath(path);
          setNativeFileName(basename(path));
          setUploadError('');
        }
        return;
      } catch {
        // Native dialog unavailable: fall back to the browser file picker below.
      }
    }
    fileInputRef.current?.click();
  };

  const handleStart = () => {
    if (localPath.trim()) return handleSubmitLocalPath();
    if (file) return handleSubmit();
  };

  const isProcessing =
    !!activeJobId &&
    jobStatus !== 'idle' &&
    jobStatus !== 'completed' &&
    jobStatus !== 'failed';

  const handleCancel = async () => {
    if (!activeJobId || !isProcessing || actionBusy) return;

    setActionBusy('cancel');
    try {
      const response = await cancelJob(activeJobId);
      const detail = response.detail ?? 'Cancellation requested.';
      updateProgress(jobStatus, currentProgress, detail);
    } catch (error) {
      setUploadError(await getUploadErrorMessage(error));
    } finally {
      setActionBusy(null);
    }
  };

  const handleOpenOutputFolder = async () => {
    if (!activeJobId || actionBusy) return;

    setActionBusy('open-folder');
    try {
      await openOutputFolder(activeJobId);
    } catch (error) {
      setUploadError(await getUploadErrorMessage(error));
    } finally {
      setActionBusy(null);
    }
  };

  return (
    <div className={styles.root}>
      <div className={styles.section}>
        <Text className={styles.sectionTitle}>Files</Text>
        <div className={styles.row}>
          <Label>Input video:</Label>
          <Input value={file?.name ?? nativeFileName} readOnly placeholder="Select a video file..." />
          <Button appearance="secondary" onClick={handlePickVideo}>Browse…</Button>
        </div>
        <div className={styles.rowNoButton}>
          <Label>Local path:</Label>
          <Input
            value={localPath}
            onChange={(_, d) => {
              setLocalPath(d.value);
              setFile(null);
              setNativeFileName('');
            }}
            placeholder="C:\\Videos\\myfile.mp4 (no upload, direct local processing)"
          />
        </div>
        <div className={styles.dropzone}>
          <ArrowUploadRegular fontSize={36} />
          <Text block>Drag and drop a video file or click Browse</Text>
          <Text block className={styles.tiny}>Supported: mp4, mkv, avi, mov, webm</Text>
          <Text block className={styles.tiny}>Folders with only images or unsupported files can appear empty in the picker.</Text>
          <Text block className={styles.tiny}>Desktop app: Browse fills Local path automatically and output files (subtitles, burned video) are created next to the input file. Browser: files are uploaded and results stay in the app (Player tab).</Text>
          <input
            ref={fileInputRef}
            type="file"
            accept=".mp4,.mkv,.avi,.mov,.webm"
            onChange={handleFileChange}
            style={{ opacity: 0, position: 'absolute', inset: 0, cursor: 'pointer' }}
          />
          {file && <Text block>{file.name}</Text>}
        </div>
      </div>

      <div className={styles.section}>
        <Text className={styles.sectionTitle}>Whisper settings</Text>
        <div className={styles.rowNoButton}>
          <Label>Model size:</Label>
          <Select value={modelSize} onChange={(_, d) => setModelSize(d.value)}>
            {MODELS.map((m) => (
              <option key={m} value={m}>{m}</option>
            ))}
          </Select>
        </div>
        <div className={styles.rowNoButton}>
          <Label>Source language:</Label>
          <Select value={sourceLanguage} onChange={(_, d) => setSourceLanguage(d.value)}>
            {SOURCES.map((s) => (
              <option key={s} value={s}>{s}</option>
            ))}
          </Select>
        </div>
        <div className={styles.rowNoButton}>
          <Label>Processing mode:</Label>
          <Select value={processingMode} onChange={(_, d) => setProcessingMode(d.value as RequestedProcessingMode)}>
            <option value="auto">Auto</option>
            <option value="direct">Direct</option>
            <option value="queue">Queue</option>
          </Select>
        </div>
      </div>

      <div className={styles.section}>
        <Text className={styles.sectionTitle}>Execution</Text>
        <Text className={styles.hwStrong}>Processing runs through the shared .NET pipeline</Text>
        <Text className={styles.tiny}>Auto uses queue when available and falls back to direct API processing. Direct forces immediate API execution. Queue now tries to bootstrap local broker and worker infrastructure automatically, then falls back to a local in-process queue when external infrastructure is unavailable.</Text>
      </div>

      <div className={styles.section}>
        <Text className={styles.sectionTitle}>Target languages (select one or more)</Text>
        <div className={styles.languagesGrid}>
          {LANGS.map((lang) => (
            <Checkbox
              key={lang.code}
              checked={targetLangs.includes(lang.code)}
              onChange={(_, d) => toggleLang(lang.code, !!d.checked)}
              label={`${lang.name} (${lang.code})`}
            />
          ))}
        </div>
      </div>

      <div className={styles.section}>
        <Text className={styles.sectionTitle}>Options</Text>
        <Checkbox
          checked={burnSubs}
          onChange={(_, d) => setBurnSubs(!!d.checked)}
          label="Burn subtitles into video copy"
        />
        <Text block className={styles.tiny}>
          Auto uses queue when available and falls back to direct mode. Direct runs immediately in API mode. Queue tries to start local NATS and the worker automatically, then uses a local in-process queue fallback if external infrastructure cannot be started.
        </Text>
      </div>

      <div className={styles.section}>
        <Text className={styles.sectionTitle}>Actions</Text>
        <div className={styles.actions}>
          <Button
            appearance="primary"
            disabled={(!file && !localPath.trim()) || uploading || targetLangs.length === 0}
            onClick={handleStart}
          >
            {uploading
              ? (localPath.trim() ? 'Starting...' : `Uploading... ${uploadPercent}%`)
              : '▶ Start Processing'}
          </Button>
          <Button appearance="secondary" disabled={!isProcessing || !!actionBusy} onClick={handleCancel}>
            {actionBusy === 'cancel' ? 'Stopping...' : '■ Cancel'}
          </Button>
          <Button appearance="secondary" disabled={!activeJobId || !!actionBusy} onClick={handleOpenOutputFolder}>
            {actionBusy === 'open-folder' ? 'Opening...' : '📂 Open output folder'}
          </Button>
        </div>
        {uploadError && <Text block className={styles.errorText}>{uploadError}</Text>}
      </div>
    </div>
  );
}
