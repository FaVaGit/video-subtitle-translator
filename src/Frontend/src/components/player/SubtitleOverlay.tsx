import { useRef } from 'react';
import { makeStyles } from '@fluentui/react-components';
import { SubtitleCue } from '../../api/videoApi';
import { SubtitleStyle } from '../../store/playerStore';
import { useFabricSubtitles } from '../../hooks/useFabricSubtitles';

const useStyles = makeStyles({
  overlay: {
    position: 'absolute',
    top: 0,
    left: 0,
    width: '100%',
    height: '100%',
    pointerEvents: 'none',
  },
});

interface SubtitleOverlayProps {
  activeCues: SubtitleCue[];
  style: SubtitleStyle;
}

export function SubtitleOverlay({ activeCues, style }: SubtitleOverlayProps) {
  const styles = useStyles();
  const containerRef = useRef<HTMLDivElement>(null);
  useFabricSubtitles(activeCues, style, containerRef);

  return (
    <div className={styles.overlay} ref={containerRef}>
      <canvas />
    </div>
  );
}
