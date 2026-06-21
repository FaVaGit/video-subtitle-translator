import { SubtitleCue } from '../api/videoApi';

export function parseSrt(content: string): SubtitleCue[] {
  const cues: SubtitleCue[] = [];
  const blocks = content.trim().split(/\n\n+/);

  for (const block of blocks) {
    const lines = block.split('\n');
    if (lines.length < 3) continue;

    const index = parseInt(lines[0], 10);
    if (isNaN(index)) continue;

    const timeParts = lines[1].split(' --> ');
    if (timeParts.length !== 2) continue;

    const start = parseSrtTime(timeParts[0].trim());
    const end = parseSrtTime(timeParts[1].trim());
    const text = lines.slice(2).join('\n');

    cues.push({ index, start, end, text });
  }

  return cues;
}

function parseSrtTime(time: string): number {
  // Format: 00:01:23,456
  const parts = time.replace(',', '.').split(':');
  if (parts.length !== 3) return 0;

  return (
    parseFloat(parts[0]) * 3600 +
    parseFloat(parts[1]) * 60 +
    parseFloat(parts[2])
  );
}
