import {
  makeStyles,
  tokens,
  Text,
} from '@fluentui/react-components';
import {
  VideoClipRegular,
} from '@fluentui/react-icons';

const useStyles = makeStyles({
  toolbar: {
    borderBottom: `1px solid ${tokens.colorNeutralStroke2}`,
    padding: '10px 14px',
    display: 'flex',
    alignItems: 'center',
    gap: '10px',
    backgroundColor: '#fafafa',
  },
  title: {
    fontWeight: tokens.fontWeightBold,
    fontSize: '18px',
  },
  subtitle: {
    color: tokens.colorNeutralForeground3,
    fontSize: tokens.fontSizeBase200,
  },
  spacer: {
    flex: 1,
  },
});

export function CommandBar() {
  const styles = useStyles();

  return (
    <div className={styles.toolbar}>
      <VideoClipRegular fontSize={22} />
      <div>
        <Text className={styles.title}>Video Subtitle Translator</Text>
        <Text className={styles.subtitle}>Desktop-style workflow: files, settings, progress, log</Text>
      </div>
      <div className={styles.spacer} />
    </div>
  );
}
