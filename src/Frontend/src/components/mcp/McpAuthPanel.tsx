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
const TOKEN_META_KEY = 'vst.github.token.meta';

type SavedMeta = {
  login: string | null;
  expiresAt: string | null;
  savedAt: string;
};

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
  const [savedMeta, setSavedMeta] = useState<SavedMeta | null>(() => {
    if (typeof window === 'undefined') return null;
    const raw = localStorage.getItem(TOKEN_META_KEY);
    if (!raw) return null;
    try {
      return JSON.parse(raw) as SavedMeta;
    } catch {
      return null;
    }
  });
  const [hasSavedToken, setHasSavedToken] = useState<boolean>(() => {
    if (typeof window === 'undefined') return false;
    return !!localStorage.getItem(TOKEN_KEY);
  });
  const [token, setToken] = useState('');
  const [savedUser, setSavedUser] = useState<string | null>(savedMeta?.login ?? null);
  const [savedExpiry, setSavedExpiry] = useState<string | null>(savedMeta?.expiresAt ?? null);
  const [validating, setValidating] = useState(false);
  const [status, setStatus] = useState<string>('Not authenticated');

  const expiryText = useMemo(() => {
    if (!savedExpiry) return 'Not provided by GitHub for this token.';
    const date = new Date(savedExpiry);
    if (Number.isNaN(date.getTime())) return savedExpiry;
    return date.toLocaleString();
  }, [savedExpiry]);

  const expiryState = useMemo(() => {
    if (!savedExpiry) {
      return {
        label: 'Expiry unknown',
        color: 'informative' as const,
      };
    }

    const date = new Date(savedExpiry);
    if (Number.isNaN(date.getTime())) {
      return {
        label: 'Expiry format unknown',
        color: 'informative' as const,
      };
    }

    const daysLeft = Math.floor((date.getTime() - Date.now()) / (1000 * 60 * 60 * 24));
    if (daysLeft < 0) {
      return {
        label: 'Token expired',
        color: 'danger' as const,
      };
    }

    if (daysLeft <= 7) {
      return {
        label: `Expiring soon (${daysLeft}d)`,
        color: 'warning' as const,
      };
    }

    return {
      label: `Valid (${daysLeft}d left)`,
      color: 'success' as const,
    };
  }, [savedExpiry]);

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
      const expiresAt = res.headers.get('github-authentication-token-expiration');
      const expiryMsg = expiresAt ? ` | expires: ${expiresAt}` : ' | expiration header not provided';
      setStatus(`Authenticated as ${login}${expiryMsg}`);
      return { login, expiresAt };
    } catch {
      setStatus('Unable to validate token from browser network context.');
      return null;
    } finally {
      setValidating(false);
    }
  };

  const handleValidate = async () => {
    const validated = await validateToken(token);
    if (!validated) return;
    setSavedUser(validated.login);
    setSavedExpiry(validated.expiresAt);
  };

  const handleSave = async () => {
    const validated = await validateToken(token);
    if (!validated) return;

    localStorage.setItem(TOKEN_KEY, token.trim());
    const meta: SavedMeta = {
      login: validated.login,
      expiresAt: validated.expiresAt,
      savedAt: new Date().toISOString(),
    };
    localStorage.setItem(TOKEN_META_KEY, JSON.stringify(meta));

    setHasSavedToken(true);
    setSavedMeta(meta);
    setSavedUser(validated.login);
    setSavedExpiry(validated.expiresAt);
    setStatus(`Saved locally for browser session usage (${validated.login}).`);
  };

  const handleClear = () => {
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(TOKEN_META_KEY);
    setToken('');
    setHasSavedToken(false);
    setSavedMeta(null);
    setSavedUser(null);
    setSavedExpiry(null);
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
      <Text block className={styles.hint}>
        Persistent MCP authentication recommended: run gh auth login once in terminal.
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

      {(savedUser || hasSavedToken) && (
        <>
          <Text block className={styles.hint}>
            Token expiry: {expiryText}
          </Text>
          <Badge appearance="filled" color={expiryState.color}>
            {expiryState.label}
          </Badge>
        </>
      )}

      {savedMeta?.savedAt && (
        <Text block className={styles.hint}>
          Saved at: {new Date(savedMeta.savedAt).toLocaleString()}
        </Text>
      )}

      <Text block className={styles.hint}>{status}</Text>
    </div>
  );
}
