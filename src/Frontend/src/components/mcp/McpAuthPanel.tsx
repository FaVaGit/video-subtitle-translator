import { useMemo, useState } from 'react';
import {
  makeStyles,
  tokens,
  Button,
  Input,
  Field,
  Text,
  Badge,
} from '@fluentui/react-components';

const TOKEN_KEY = 'vst.github.token';

const useStyles = makeStyles({
  section: {
    border: `1px solid ${tokens.colorNeutralStroke2}`,
    borderRadius: tokens.borderRadiusMedium,
    padding: '10px',
    backgroundColor: '#ffffff',
    marginTop: '10px',
  },
  title: {
    fontWeight: tokens.fontWeightSemibold,
    marginBottom: '8px',
  },
  row: {
    display: 'flex',
    gap: '8px',
    flexWrap: 'wrap',
    alignItems: 'center',
  },
  hint: {
    color: tokens.colorNeutralForeground3,
    fontSize: tokens.fontSizeBase200,
  },
  status: {
    marginTop: '8px',
  },
});

export function McpAuthPanel() {
  const styles = useStyles();
  const [token, setToken] = useState('');
  const [savedUser, setSavedUser] = useState<string | null>(null);
  const [validating, setValidating] = useState(false);
  const [status, setStatus] = useState<string>('Not authenticated');

  const hasSavedToken = useMemo(() => {
    if (typeof window === 'undefined') return false;
    return !!localStorage.getItem(TOKEN_KEY);
  }, []);

  const openGitHubAuth = () => {
    window.open('https://github.com/settings/tokens', '_blank', 'noopener,noreferrer');
  };

  const validateToken = async (value: string) => {
    const trimmed = value.trim();
    if (!trimmed) {
      setStatus('Insert a token first.');
      return null;
    }

    setValidating(true);
    try {
      const res = await fetch('https://api.github.com/user', {
        headers: {
          Authorization: `Bearer ${trimmed}`,
          Accept: 'application/vnd.github+json',
        },
      });

      if (!res.ok) {
        setStatus(`Token invalid (${res.status}).`);
        return null;
      }

      const json = await res.json();
      const login = json?.login ? String(json.login) : 'authenticated-user';
      setStatus(`Authenticated as ${login}`);
      return login;
    } catch {
      setStatus('Unable to validate token from browser network context.');
      return null;
    } finally {
      setValidating(false);
    }
  };

  const handleValidate = async () => {
    const login = await validateToken(token);
    if (login) setSavedUser(login);
  };

  const handleSave = async () => {
    const login = await validateToken(token);
    if (!login) return;
    localStorage.setItem(TOKEN_KEY, token.trim());
    setSavedUser(login);
    setStatus(`Saved locally for browser session usage (${login}).`);
  };

  const handleClear = () => {
    localStorage.removeItem(TOKEN_KEY);
    setToken('');
    setSavedUser(null);
    setStatus('Authentication cleared from local storage.');
  };

  return (
    <div className={styles.section}>
      <Text className={styles.title}>MCP & GitHub Authentication</Text>
      <Text block className={styles.hint}>
        Authenticate from UI by pasting a GitHub token. This enables GitHub-authenticated AI features.
      </Text>
      <Text block className={styles.hint}>
        For MCP runtime, then start mode mcp from launcher (scripts/run.bat mcp).
      </Text>

      <Field label="GitHub token (PAT or fine-grained token)">
        <Input
          type="password"
          value={token}
          onChange={(_, d) => setToken(d.value)}
          placeholder="ghp_xxx / github_pat_xxx"
        />
      </Field>

      <div className={styles.row}>
        <Button appearance="secondary" onClick={openGitHubAuth}>
          Open GitHub token page
        </Button>
        <Button appearance="secondary" onClick={handleValidate} disabled={validating}>
          Validate token
        </Button>
        <Button appearance="primary" onClick={handleSave} disabled={validating}>
          Save token locally
        </Button>
        <Button appearance="subtle" onClick={handleClear}>
          Clear
        </Button>
      </div>

      <div className={styles.status}>
        <Badge appearance="filled" color={savedUser || hasSavedToken ? 'success' : 'informative'}>
          {savedUser || hasSavedToken ? 'Authenticated (local)' : 'Not authenticated'}
        </Badge>
      </div>

      <Text block className={styles.hint}>{status}</Text>
    </div>
  );
}
