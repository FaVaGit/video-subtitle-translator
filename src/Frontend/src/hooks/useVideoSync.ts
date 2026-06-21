import { useEffect, useRef, useCallback } from 'react';
import { SubtitleCue } from '../api/videoApi';
import { usePlayerStore } from '../store/playerStore';

export function useVideoSync(cues: SubtitleCue[]) {
  const videoRef = useRef<HTMLVideoElement>(null);
  const setActiveCues = usePlayerStore((s) => s.setActiveCues);
  const setCurrentTime = usePlayerStore((s) => s.setCurrentTime);
  const setDuration = usePlayerStore((s) => s.setDuration);

  const handleTimeUpdate = useCallback(() => {
    const video = videoRef.current;
    if (!video) return;

    const t = video.currentTime;
    setCurrentTime(t);

    const active = cues.filter((c) => t >= c.start && t <= c.end);
    setActiveCues(active);
  }, [cues, setActiveCues, setCurrentTime]);

  useEffect(() => {
    const video = videoRef.current;
    if (!video) return;

    video.addEventListener('timeupdate', handleTimeUpdate);
    video.addEventListener('loadedmetadata', () => setDuration(video.duration));

    return () => {
      video.removeEventListener('timeupdate', handleTimeUpdate);
    };
  }, [handleTimeUpdate, setDuration]);

  return videoRef;
}
