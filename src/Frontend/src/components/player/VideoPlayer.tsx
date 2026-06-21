import { useEffect, useState } from 'react';
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
  const videoRef = useVideoSync(cues);

  useEffect(() => {
    if (!jobId) return;
    getTracks(jobId).then(setTracks);
  }, [jobId]);

  useEffect(() => {
    if (!jobId || !currentLang) return;
    getSubtitleCues(jobId, currentLang).then(setCues);
  }, [jobId, currentLang]);

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

  if (!jobId) return <Text>No video loaded. Complete a transcription first.</Text>;

  return (
    <Card className={styles.container}>
      <div className={styles.videoWrapper}>
        <video
          ref={videoRef}
          className={styles.video}
          src={getVideoStreamUrl(jobId)}
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

      {(mode === 'panel' || mode === 'dual') && (
        <SubtitlePanel activeCues={activeCues} />
      )}
    </Card>
  );
}
