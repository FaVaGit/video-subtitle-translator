import { makeStyles, tokens, Text } from '@fluentui/react-components';
import { SubtitleCue } from '../../api/videoApi';

const useStyles = makeStyles({
  panel: {
    padding: '16px',
    borderTop: `1px solid ${tokens.colorNeutralStroke1}`,
    backgroundColor: tokens.colorNeutralBackground2,
    minHeight: '60px',
    display: 'flex',
    flexDirection: 'column',
    alignItems: 'center',
    justifyContent: 'center',
  },
});

interface SubtitlePanelProps {
  activeCues: SubtitleCue[];
}

export function SubtitlePanel({ activeCues }: SubtitlePanelProps) {
  const styles = useStyles();

  return (
    <div className={styles.panel}>
      {activeCues.length === 0 ? (
        <Text italic size={300}>...</Text>
      ) : (
        activeCues.map((cue) => (
          <Text key={cue.index} size={400} weight="semibold">
            {cue.text}
          </Text>
        ))
      )}
    </div>
  );
}
