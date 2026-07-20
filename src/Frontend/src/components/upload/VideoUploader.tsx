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
import { uploadVideo } from '../../api/videoApi';
import { useJobStore } from '../../store/jobStore';

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

function getUploadErrorMessage(error: unknown): string {
  if (axios.isAxiosError(error)) {
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

  return 'Upload failed. Verify that the backend and queue are available, then retry.';
}

const MODELS = ['tiny', 'base', 'small', 'medium', 'large-v3'];
const SOURCES = [
  'auto', 'en', 'it', 'fr', 'de', 'es', 'pt', 'ja', 'zh', 'ko', 'ru', 'ar', 'hi',
];
const LANGS = [
  { code: 'en', name: 'English' },
  { code: 'it', name: 'Italiano' },
  { code: 'fr', name: 'Français' },
  { code: 'de', name: 'Deutsch' },
  { code: 'es', name: 'Español' },
  { code: 'pt', name: 'Português' },
  { code: 'ja', name: '日本語' },
  { code: 'zh', name: '中文' },
  { code: 'ko', name: '한국어' },
  { code: 'ru', name: 'Русский' },
  { code: 'ar', name: 'العربية' },
];

export function VideoUploader() {
  const styles = useStyles();
  const [file, setFile] = useState<File | null>(null);
  const [sourceLanguage, setSourceLanguage] = useState('auto');
  const [targetLangs, setTargetLangs] = useState<string[]>(['it', 'en']);
  const [modelSize, setModelSize] = useState('medium');
  const [burnSubs, setBurnSubs] = useState(false);
  const [uploading, setUploading] = useState(false);
  const [uploadError, setUploadError] = useState('');
  const fileInputRef = useRef<HTMLInputElement | null>(null);
  const setJob = useJobStore((s) => s.setJob);

  const handleFileChange = useCallback((e: React.ChangeEvent<HTMLInputElement>) => {
    const selected = e.target.files?.[0];
    if (selected) {
      setFile(selected);
      setUploadError('');
    }
  }, []);

  const handleSubmit = async () => {
    if (!file) return;
    if (targetLangs.length === 0) return;
    setUploading(true);
    setUploadError('');
    try {
      const result = await uploadVideo(file, {
        sourceLanguage: sourceLanguage === 'auto' ? undefined : sourceLanguage,
        targetLanguages: targetLangs.join(','),
        modelSize,
        burnSubtitles: burnSubs,
      });
      setJob(result.jobId);
    } catch (error) {
      setUploadError(getUploadErrorMessage(error));
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

  const handlePickVideo = () => {
    fileInputRef.current?.click();
  };

  return (
    <div className={styles.root}>
      <div className={styles.section}>
        <Text className={styles.sectionTitle}>Files</Text>
        <div className={styles.row}>
          <Label>Input video:</Label>
          <Input value={file?.name ?? ''} readOnly placeholder="Select a video file..." />
          <Button appearance="secondary" onClick={handlePickVideo}>Browse…</Button>
        </div>
        <div className={styles.dropzone}>
          <ArrowUploadRegular fontSize={36} />
          <Text block>Drag and drop a video file or click Browse</Text>
          <Text block className={styles.tiny}>Supported: mp4, mkv, avi, mov, webm</Text>
          <Text block className={styles.tiny}>Folders with only images or unsupported files can appear empty in the picker.</Text>
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
      </div>

      <div className={styles.section}>
        <Text className={styles.sectionTitle}>Hardware (auto-detected)</Text>
        <Text className={styles.hwStrong}>🟢 WEB - Browser frontend connected to API worker</Text>
        <Text className={styles.tiny}>Backend processing uses API/Worker runtime and server-side resources</Text>
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
      </div>

      <div className={styles.section}>
        <Text className={styles.sectionTitle}>Actions</Text>
        <div className={styles.actions}>
          <Button appearance="primary" disabled={!file || uploading || targetLangs.length === 0} onClick={handleSubmit}>
            {uploading ? 'Uploading...' : '▶ Start Processing'}
          </Button>
          <Button appearance="secondary" disabled>
            ■ Cancel
          </Button>
          <Button appearance="secondary" disabled>
            📂 Open output folder
          </Button>
        </div>
        {uploadError && <Text block className={styles.errorText}>{uploadError}</Text>}
      </div>
    </div>
  );
}
