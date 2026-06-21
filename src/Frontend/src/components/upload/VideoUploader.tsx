import { useState, useCallback } from 'react';
import {
  makeStyles,
  tokens,
  Card,
  CardHeader,
  Button,
  Input,
  Select,
  Checkbox,
  Field,
  Text,
} from '@fluentui/react-components';
import { ArrowUploadRegular } from '@fluentui/react-icons';
import { uploadVideo } from '../../api/videoApi';
import { useJobStore } from '../../store/jobStore';

const useStyles = makeStyles({
  card: {
    maxWidth: '600px',
    margin: '0 auto',
  },
  form: {
    display: 'flex',
    flexDirection: 'column',
    gap: '12px',
    padding: '16px',
  },
  dropzone: {
    border: `2px dashed ${tokens.colorNeutralStroke1}`,
    borderRadius: tokens.borderRadiusMedium,
    padding: '32px',
    textAlign: 'center' as const,
    cursor: 'pointer',
    ':hover': {
      borderTopColor: tokens.colorBrandStroke1,
      borderRightColor: tokens.colorBrandStroke1,
      borderBottomColor: tokens.colorBrandStroke1,
      borderLeftColor: tokens.colorBrandStroke1,
      backgroundColor: tokens.colorNeutralBackground1Hover,
    },
  },
});

const MODELS = ['tiny', 'base', 'small', 'medium', 'large-v3'];

export function VideoUploader() {
  const styles = useStyles();
  const [file, setFile] = useState<File | null>(null);
  const [targetLangs, setTargetLangs] = useState('en');
  const [modelSize, setModelSize] = useState('medium');
  const [burnSubs, setBurnSubs] = useState(false);
  const [uploading, setUploading] = useState(false);
  const setJob = useJobStore((s) => s.setJob);

  const handleFileChange = useCallback((e: React.ChangeEvent<HTMLInputElement>) => {
    const selected = e.target.files?.[0];
    if (selected) setFile(selected);
  }, []);

  const handleSubmit = async () => {
    if (!file) return;
    setUploading(true);
    try {
      const result = await uploadVideo(file, {
        targetLanguages: targetLangs,
        modelSize,
        burnSubtitles: burnSubs,
      });
      setJob(result.jobId);
    } finally {
      setUploading(false);
    }
  };

  return (
    <Card className={styles.card}>
      <CardHeader header={<Text weight="semibold">Upload Video</Text>} />
      <div className={styles.form}>
        <div className={styles.dropzone}>
          <ArrowUploadRegular fontSize={48} />
          <Text block>Drag & drop a video file or click to browse</Text>
          <input
            type="file"
            accept=".mp4,.mkv,.avi,.mov,.webm"
            onChange={handleFileChange}
            style={{ opacity: 0, position: 'absolute', inset: 0, cursor: 'pointer' }}
          />
          {file && <Text block>{file.name}</Text>}
        </div>

        <Field label="Target Languages (comma-separated)">
          <Input value={targetLangs} onChange={(_, d) => setTargetLangs(d.value)} />
        </Field>

        <Field label="Model Size">
          <Select value={modelSize} onChange={(_, d) => setModelSize(d.value)}>
            {MODELS.map((m) => (
              <option key={m} value={m}>{m}</option>
            ))}
          </Select>
        </Field>

        <Checkbox
          checked={burnSubs}
          onChange={(_, d) => setBurnSubs(!!d.checked)}
          label="Burn subtitles into video"
        />

        <Button
          appearance="primary"
          disabled={!file || uploading}
          onClick={handleSubmit}
        >
          {uploading ? 'Uploading...' : 'Start Processing'}
        </Button>
      </div>
    </Card>
  );
}
