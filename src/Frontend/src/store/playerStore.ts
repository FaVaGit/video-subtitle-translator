import { create } from 'zustand';
import { SubtitleCue } from '../api/videoApi';

export type PlayerMode = 'overlay' | 'panel' | 'dual';

export interface SubtitleStyle {
  fontSize: number;
  fontFamily: string;
  color: string;
  outlineColor: string;
  outlineWidth: number;
  bottomMargin: number;
  lineHeight: number;
}

interface PlayerState {
  jobId: string | null;
  isPlaying: boolean;
  currentTime: number;
  duration: number;
  mode: PlayerMode;
  activeCues: SubtitleCue[];
  currentLanguage: string;
  style: SubtitleStyle;
  setJobId: (jobId: string) => void;
  setPlaying: (playing: boolean) => void;
  setCurrentTime: (time: number) => void;
  setDuration: (duration: number) => void;
  setMode: (mode: PlayerMode) => void;
  setActiveCues: (cues: SubtitleCue[]) => void;
  setLanguage: (lang: string) => void;
  setStyle: (style: Partial<SubtitleStyle>) => void;
}

const defaultStyle: SubtitleStyle = {
  fontSize: 24,
  fontFamily: 'Arial, sans-serif',
  color: '#ffffff',
  outlineColor: '#000000',
  outlineWidth: 2,
  bottomMargin: 40,
  lineHeight: 32,
};

export const usePlayerStore = create<PlayerState>((set) => ({
  jobId: null,
  isPlaying: false,
  currentTime: 0,
  duration: 0,
  mode: 'overlay',
  activeCues: [],
  currentLanguage: 'en',
  style: defaultStyle,
  setJobId: (jobId) => set({ jobId }),
  setPlaying: (isPlaying) => set({ isPlaying }),
  setCurrentTime: (currentTime) => set({ currentTime }),
  setDuration: (duration) => set({ duration }),
  setMode: (mode) => set({ mode }),
  setActiveCues: (activeCues) => set({ activeCues }),
  setLanguage: (currentLanguage) => set({ currentLanguage }),
  setStyle: (partial) => set((state) => ({ style: { ...state.style, ...partial } })),
}));
