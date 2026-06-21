import {
  Toolbar,
  ToolbarButton,
  makeStyles,
  tokens,
  Text,
} from '@fluentui/react-components';
import {
  VideoClipRegular,
  DarkThemeRegular,
} from '@fluentui/react-icons';

const useStyles = makeStyles({
  toolbar: {
    borderBottom: `1px solid ${tokens.colorNeutralStroke1}`,
    padding: '4px 16px',
    display: 'flex',
    alignItems: 'center',
    gap: '12px',
  },
  title: {
    fontWeight: tokens.fontWeightSemibold,
    fontSize: tokens.fontSizeBase400,
  },
  spacer: {
    flex: 1,
  },
});

export function CommandBar() {
  const styles = useStyles();

  return (
    <div className={styles.toolbar}>
      <VideoClipRegular fontSize={24} />
      <Text className={styles.title}>Video Subtitle Translator</Text>
      <div className={styles.spacer} />
      <Toolbar>
        <ToolbarButton icon={<DarkThemeRegular />} aria-label="Toggle theme" />
      </Toolbar>
    </div>
  );
}
