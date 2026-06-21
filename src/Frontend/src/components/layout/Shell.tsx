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
import { useJobStore } from '../../store/jobStore';

const useStyles = makeStyles({
  root: {
    display: 'flex',
    flexDirection: 'column',
    height: '100vh',
    backgroundColor: tokens.colorNeutralBackground1,
  },
  content: {
    flex: 1,
    overflow: 'auto',
    padding: '16px',
  },
});

export function Shell() {
  const styles = useStyles();
  const [activeTab, setActiveTab] = useState('upload');
  const jobStatus = useJobStore((s) => s.status);

  const handleTabSelect = (_: SelectTabEvent, data: SelectTabData) => {
    setActiveTab(data.value as string);
  };

  return (
    <div className={styles.root}>
      <CommandBar />
      <TabList selectedValue={activeTab} onTabSelect={handleTabSelect}>
        <Tab value="upload">Upload & Process</Tab>
        <Tab value="player" disabled={jobStatus !== 'completed'}>
          Player
        </Tab>
      </TabList>
      <div className={styles.content}>
        {activeTab === 'upload' && (
          <>
            <VideoUploader />
            <JobProgress />
          </>
        )}
        {activeTab === 'player' && <VideoPlayer />}
      </div>
    </div>
  );
}
