import { spawn } from 'node:child_process';
import fs from 'node:fs/promises';
import path from 'node:path';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { StdioServerTransport } from '@modelcontextprotocol/sdk/server/stdio.js';
import { z } from 'zod';

const server = new McpServer({
  name: 'video-subtitle-translator-mcp',
  version: '1.0.0',
});

function runCommand(cmd, args, cwd = process.cwd()) {
  return new Promise((resolve, reject) => {
    const child = spawn(cmd, args, { cwd, shell: false });
    let stdout = '';
    let stderr = '';

    child.stdout.on('data', (d) => {
      stdout += d.toString();
    });

    child.stderr.on('data', (d) => {
      stderr += d.toString();
    });

    child.on('error', (err) => reject(err));
    child.on('close', (code) => {
      if (code !== 0) {
        reject(new Error(`${cmd} exited with code ${code}: ${stderr || stdout}`));
        return;
      }
      resolve({ stdout, stderr });
    });
  });
}

function ensureAbsolute(p) {
  return path.isAbsolute(p) ? p : path.resolve(process.cwd(), p);
}

server.tool(
  'video_probe',
  'Reads video metadata (duration, codecs, streams) using ffprobe.',
  {
    videoPath: z.string().describe('Absolute or relative path to the input video file.'),
  },
  async ({ videoPath }) => {
    const abs = ensureAbsolute(videoPath);
    const result = await runCommand('ffprobe', [
      '-v',
      'error',
      '-print_format',
      'json',
      '-show_format',
      '-show_streams',
      abs,
    ]);

    return {
      content: [
        {
          type: 'text',
          text: result.stdout.trim(),
        },
      ],
    };
  }
);

server.tool(
  'video_extract_frame',
  'Extracts one frame from a video at the requested timestamp.',
  {
    videoPath: z.string().describe('Path to source video.'),
    timestamp: z.string().describe('Timestamp like 00:00:12.500 or 12.5.'),
    outputImagePath: z.string().describe('Output image path, e.g. ./out/frame.png.'),
  },
  async ({ videoPath, timestamp, outputImagePath }) => {
    const input = ensureAbsolute(videoPath);
    const out = ensureAbsolute(outputImagePath);
    await fs.mkdir(path.dirname(out), { recursive: true });

    await runCommand('ffmpeg', [
      '-y',
      '-ss',
      timestamp,
      '-i',
      input,
      '-frames:v',
      '1',
      out,
    ]);

    return {
      content: [
        {
          type: 'text',
          text: `Frame extracted to: ${out}`,
        },
      ],
    };
  }
);

server.tool(
  'video_extract_audio',
  'Extracts mono 16k WAV audio from a video for downstream transcription.',
  {
    videoPath: z.string().describe('Path to source video.'),
    outputWavPath: z.string().describe('Output wav path, e.g. ./out/audio.wav.'),
  },
  async ({ videoPath, outputWavPath }) => {
    const input = ensureAbsolute(videoPath);
    const out = ensureAbsolute(outputWavPath);
    await fs.mkdir(path.dirname(out), { recursive: true });

    await runCommand('ffmpeg', [
      '-y',
      '-i',
      input,
      '-vn',
      '-acodec',
      'pcm_s16le',
      '-ar',
      '16000',
      '-ac',
      '1',
      out,
    ]);

    return {
      content: [
        {
          type: 'text',
          text: `Audio extracted to: ${out}`,
        },
      ],
    };
  }
);

server.tool(
  'github_models_chat',
  'Calls GitHub Models with authenticated GitHub token for AI assistance on video content analysis.',
  {
    prompt: z.string().describe('User prompt for the model.'),
    system: z.string().optional().describe('Optional system prompt.'),
    model: z
      .string()
      .default('gpt-4.1')
      .describe('GitHub Models model id, e.g. gpt-4.1, gpt-4o-mini, phi-4.'),
  },
  async ({ prompt, system, model }) => {
    const token = process.env.GITHUB_TOKEN;
    if (!token) {
      return {
        content: [
          {
            type: 'text',
            text: 'Missing GITHUB_TOKEN. Authenticate with GitHub first (e.g. gh auth login) and set GITHUB_TOKEN, or use scripts/run-mcp.bat which tries to import token from gh auth token.',
          },
        ],
      };
    }

    const body = {
      model,
      messages: [
        ...(system ? [{ role: 'system', content: system }] : []),
        { role: 'user', content: prompt },
      ],
      temperature: 0.2,
    };

    const res = await fetch('https://models.inference.ai.azure.com/chat/completions', {
      method: 'POST',
      headers: {
        Authorization: `Bearer ${token}`,
        'Content-Type': 'application/json',
      },
      body: JSON.stringify(body),
    });

    if (!res.ok) {
      const txt = await res.text();
      return {
        content: [
          {
            type: 'text',
            text: `GitHub Models request failed (${res.status}): ${txt}`,
          },
        ],
      };
    }

    const json = await res.json();
    const answer = json?.choices?.[0]?.message?.content ?? JSON.stringify(json);

    return {
      content: [
        {
          type: 'text',
          text: String(answer),
        },
      ],
    };
  }
);

async function main() {
  const transport = new StdioServerTransport();
  await server.connect(transport);
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
