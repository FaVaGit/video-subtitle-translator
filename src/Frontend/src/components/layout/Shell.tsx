import { useState } from 'react';
import {
  makeStyles,
  tokens,
  TabList,
  Tab,
  SelectTabEvent,
  SelectTabData,
} from '@fluentui/react-components';
import { VideoUploader } from '../upload/VideoUploader';
import { JobProgress } from '../processing/JobProgress';
import { VideoPlayer } from '../player/VideoPlayer';
import { CommandBar } from './CommandBar';
import { McpAuthPanel } from '../mcp/McpAuthPanel';

const useStyles = makeStyles({
  root: {
    display: 'flex',
    flexDirection: 'column',
    minHeight: '100vh',
    backgroundColor: '#f5f5f5',
    fontFamily: 'Segoe UI, Tahoma, sans-serif',
  },
  shell: {
    width: '100%',
    maxWidth: '1100px',
    margin: '12px auto',
    backgroundColor: '#ffffff',
    border: `1px solid ${tokens.colorNeutralStroke2}`,
    borderRadius: tokens.borderRadiusMedium,
    overflow: 'hidden',
    boxShadow: '0 2px 10px rgba(0,0,0,0.06)',
  },
  content: {
    flex: 1,
    overflow: 'auto',
    padding: '12px',
  },
  tabs: {
    borderBottom: `1px solid ${tokens.colorNeutralStroke1}`,
    padding: '0 8px',
  },
});

export function Shell() {
  const styles = useStyles();
  const [activeTab, setActiveTab] = useState('upload');

  const handleTabSelect = (_: SelectTabEvent, data: SelectTabData) => {
    setActiveTab(data.value as string);
  };

  return (
    <div className={styles.root}>
      <div className={styles.shell}>
        <CommandBar />
        <TabList className={styles.tabs} selectedValue={activeTab} onTabSelect={handleTabSelect}>
          <Tab value="upload">🎬 Transcribe</Tab>
          <Tab value="player">
            ▶ Player
          </Tab>
        </TabList>
        <div className={styles.content}>
          {activeTab === 'upload' && (
            <>
              <VideoUploader />
              <JobProgress />
              <McpAuthPanel />
            </>
          )}
          {activeTab === 'player' && <VideoPlayer />}
        </div>
      </div>
    </div>
  );
}
