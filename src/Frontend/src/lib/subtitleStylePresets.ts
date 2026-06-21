import { tokens } from '@fluentui/react-components';
import { SubtitleStyle } from '../store/playerStore';

export const subtitlePresets: Record<string, SubtitleStyle> = {
  default: {
    fontSize: 24,
    fontFamily: 'Arial, sans-serif',
    color: '#ffffff',
    outlineColor: '#000000',
    outlineWidth: 2,
    bottomMargin: 40,
    lineHeight: 32,
  },
  netflix: {
    fontSize: 28,
    fontFamily: '"Netflix Sans", Helvetica, sans-serif',
    color: '#ffffff',
    outlineColor: 'transparent',
    outlineWidth: 0,
    bottomMargin: 48,
    lineHeight: 36,
  },
  youtube: {
    fontSize: 20,
    fontFamily: 'Roboto, sans-serif',
    color: '#ffffff',
    outlineColor: '#000000',
    outlineWidth: 1,
    bottomMargin: 32,
    lineHeight: 28,
  },
  highContrast: {
    fontSize: 26,
    fontFamily: 'Arial, sans-serif',
    color: '#ffff00',
    outlineColor: '#000000',
    outlineWidth: 3,
    bottomMargin: 40,
    lineHeight: 34,
  },
};
