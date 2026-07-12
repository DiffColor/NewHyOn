import './styles.css';
import { NewHyOnPlayerApp } from './app/newhyon-player-app';
import {
  enableDurableLocalStorageMirror,
  flushDurableLocalStorage,
  restoreDurableLocalStorage,
} from './app/durable-local-storage';
import { resolveRuntimeConfig } from './app/runtime-config';

async function ensureSamsungWebApis(): Promise<void> {
  if (window.webapis) {
    return;
  }

  const existingScript = document.querySelector<HTMLScriptElement>('script[src="$WEBAPIS/webapis/webapis.js"]');
  await new Promise<void>((resolve) => {
    if (existingScript) {
      existingScript.addEventListener('load', () => resolve(), { once: true });
      existingScript.addEventListener('error', () => resolve(), { once: true });
    } else {
      const script = document.createElement('script');
      script.src = '$WEBAPIS/webapis/webapis.js';
      script.async = true;
      script.onload = () => resolve();
      script.onerror = () => resolve();
      document.head.appendChild(script);
    }

    window.setTimeout(resolve, 3000);
  });
}

async function bootstrap(): Promise<void> {
  try {
    await ensureSamsungWebApis();
    await restoreDurableLocalStorage();
    enableDurableLocalStorageMirror();
    window.addEventListener('pagehide', () => {
      void flushDurableLocalStorage();
    });
    window.addEventListener('beforeunload', () => {
      void flushDurableLocalStorage();
    });
    const config = await resolveRuntimeConfig();
    const app = new NewHyOnPlayerApp(config);
    await app.start();
  } catch (error) {
    const message = error instanceof Error ? error.message : String(error);
    const state = document.querySelector('#status-state');
    const statusMessage = document.querySelector('#status-message');
    const loadingOverlay = document.querySelector('#loading-overlay');
    state && (state.textContent = 'error');
    statusMessage && (statusMessage.textContent = message);
    loadingOverlay?.classList.add('loading-overlay--hidden');
    console.error(message);
  }
}

void bootstrap();
