import { useEffect, useRef, useState } from 'react';
import {
  makeStyles,
  tokens,
  Card,
  Select,
  Button,
  ToggleButton,
  Toolbar,
  ToolbarButton,
  Text,
  Field,
} from '@fluentui/react-components';
import {
  PlayRegular,
  PauseRegular,
  FullScreenMaximizeRegular,
  SubtitlesRegular,
} from '@fluentui/react-icons';
import { getVideoStreamUrl, getTracks, getSubtitleCues, SubtitleCue, SubtitleTrack } from '../../api/videoApi';
import { usePlayerStore } from '../../store/playerStore';
import { useVideoSync } from '../../hooks/useVideoSync';
import { SubtitleOverlay } from './SubtitleOverlay';
import { SubtitlePanel } from './SubtitlePanel';

const useStyles = makeStyles({
  container: {
    maxWidth: '960px',
    margin: '0 auto',
  },
  videoWrapper: {
    position: 'relative',
    backgroundColor: '#000',
    borderRadius: tokens.borderRadiusMedium,
    overflow: 'hidden',
  },
  video: {
    width: '100%',
    display: 'block',
  },
  controls: {
    display: 'flex',
    alignItems: 'center',
    gap: '8px',
    padding: '8px 16px',
    borderTop: `1px solid ${tokens.colorNeutralStroke1}`,
  },
  spacer: {
    flex: 1,
  },
  trackBar: {
    display: 'flex',
    alignItems: 'center',
    gap: '8px',
    padding: '8px 16px',
  },
});

export function VideoPlayer() {
  const styles = useStyles();
  const { jobId, mode, activeCues, style, setMode, setPlaying, isPlaying } = usePlayerStore();
  const [tracks, setTracks] = useState<SubtitleTrack[]>([]);
  const [cues, setCues] = useState<SubtitleCue[]>([]);
  const [currentLang, setCurrentLang] = useState('en');
  const [localVideoUrl, setLocalVideoUrl] = useState<string | null>(null);
  const [localVideoName, setLocalVideoName] = useState<string>('');
  const fileInputRef = useRef<HTMLInputElement | null>(null);
  const videoRef = useVideoSync(cues);

  const videoSrc = localVideoUrl ?? (jobId ? getVideoStreamUrl(jobId) : null);

  useEffect(() => {
    if (localVideoUrl) {
      setTracks([]);
      setCues([]);
      return;
    }

    if (!jobId) return;
    getTracks(jobId).then(setTracks);
  }, [jobId, localVideoUrl]);

  useEffect(() => {
    if (localVideoUrl) return;
    if (!jobId || !currentLang) return;
    getSubtitleCues(jobId, currentLang).then(setCues);
  }, [jobId, currentLang, localVideoUrl]);

  useEffect(() => {
    return () => {
      if (localVideoUrl) URL.revokeObjectURL(localVideoUrl);
    };
  }, [localVideoUrl]);

  const handlePickLocalVideo = () => {
    fileInputRef.current?.click();
  };

  const handleLocalVideoSelected = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;

    if (localVideoUrl) URL.revokeObjectURL(localVideoUrl);

    const nextUrl = URL.createObjectURL(file);
    setLocalVideoUrl(nextUrl);
    setLocalVideoName(file.name);
    setCurrentLang('en');
  };

  const clearLocalVideo = () => {
    if (localVideoUrl) URL.revokeObjectURL(localVideoUrl);
    setLocalVideoUrl(null);
    setLocalVideoName('');
    setTracks([]);
    setCues([]);
  };

  const togglePlayPause = () => {
    const video = videoRef.current;
    if (!video) return;
    if (video.paused) {
      video.play();
      setPlaying(true);
    } else {
      video.pause();
      setPlaying(false);
    }
  };

  const toggleFullscreen = () => {
    const wrapper = videoRef.current?.parentElement;
    if (wrapper?.requestFullscreen) wrapper.requestFullscreen();
  };

  return (
    <Card className={styles.container}>
      <div className={styles.trackBar}>
        <Field label="Local video">
          <input
            ref={fileInputRef}
            type="file"
            accept=".mp4,.mkv,.avi,.mov,.webm,.wmv,.flv"
            onChange={handleLocalVideoSelected}
            style={{ display: 'none' }}
          />
          <Button onClick={handlePickLocalVideo} appearance="secondary">
            Browse local video
          </Button>
        </Field>
        {localVideoName && <Text>{localVideoName}</Text>}
        {localVideoUrl && (
          <Button appearance="subtle" onClick={clearLocalVideo}>
            Use processed video instead
          </Button>
        )}
      </div>

      {!videoSrc && (
        <div className={styles.trackBar}>
          <Text>No video selected yet. Choose a local file or process one from Transcribe.</Text>
        </div>
      )}

      <div className={styles.videoWrapper}>
        <video
          ref={videoRef}
          className={styles.video}
          src={videoSrc ?? undefined}
        />
        {(mode === 'overlay' || mode === 'dual') && (
          <SubtitleOverlay activeCues={activeCues} style={style} />
        )}
      </div>

      <div className={styles.controls}>
        <Button
          icon={isPlaying ? <PauseRegular /> : <PlayRegular />}
          onClick={togglePlayPause}
          appearance="subtle"
        />
        <div className={styles.spacer} />
        <Toolbar>
          <ToolbarButton
            icon={<FullScreenMaximizeRegular />}
            onClick={toggleFullscreen}
          />
        </Toolbar>
      </div>

      {!localVideoUrl && tracks.length > 0 && (
        <div className={styles.trackBar}>
          <SubtitlesRegular />
          <Select
            value={currentLang}
            onChange={(_, d) => setCurrentLang(d.value)}
          >
            {tracks.map((t) => (
              <option key={t.language} value={t.language}>{t.label}</option>
            ))}
          </Select>
          <ToggleButton
            checked={mode === 'overlay'}
            onClick={() => setMode(mode === 'overlay' ? 'panel' : 'overlay')}
          >
            {mode === 'overlay' ? 'Overlay' : 'Panel'}
          </ToggleButton>
        </div>
      )}

      {(mode === 'panel' || mode === 'dual') && (
        <SubtitlePanel activeCues={activeCues} />
      )}
    </Card>
  );
}
