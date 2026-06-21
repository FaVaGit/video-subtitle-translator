import { useEffect, useRef } from 'react';
import { Canvas, FabricText } from 'fabric';
import { SubtitleCue } from '../api/videoApi';
import { SubtitleStyle } from '../store/playerStore';

export function useFabricSubtitles(
  activeCues: SubtitleCue[],
  style: SubtitleStyle,
  containerRef: React.RefObject<HTMLDivElement | null>
) {
  const canvasRef = useRef<Canvas | null>(null);

  useEffect(() => {
    if (!containerRef.current) return;

    const canvas = new Canvas(containerRef.current.querySelector('canvas')!, {
      selection: false,
    });
    canvasRef.current = canvas;

    return () => {
      canvas.dispose();
    };
  }, [containerRef]);

  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas) return;

    canvas.clear();

    activeCues.forEach((cue, i) => {
      const text = new FabricText(cue.text, {
        left: canvas.width! / 2,
        top: canvas.height! - style.bottomMargin - i * style.lineHeight,
        fontSize: style.fontSize,
        fontFamily: style.fontFamily,
        fill: style.color,
        stroke: style.outlineColor,
        strokeWidth: style.outlineWidth,
        textAlign: 'center',
        originX: 'center',
        selectable: false,
        evented: false,
      });
      canvas.add(text);
    });

    canvas.renderAll();
  }, [activeCues, style]);

  return canvasRef;
}
