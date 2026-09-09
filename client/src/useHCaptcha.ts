import { useCallback, useEffect, useRef } from 'react';
import type { HCaptchaApi, HCaptchaExecutor } from './types';

const SCRIPT_ID = 'pluperfect-hcaptcha';
const SCRIPT_SRC = 'https://js.hcaptcha.com/1/api.js?render=explicit';

/**
 * One in-flight load shared by every hook instance on the page.
 *
 * Two forms on one page would otherwise each append a script tag. The element id guards against
 * a second tag, but not against a second caller proceeding before the first has finished loading.
 */
let loading: Promise<void> | null = null;

function loadScript(): Promise<void> {
  if (typeof document === 'undefined') {
    return Promise.reject(new Error('hCaptcha needs a browser.'));
  }

  if (window.hcaptcha) return Promise.resolve();
  if (loading) return loading;

  loading = new Promise<void>((resolve, reject) => {
    const existing = document.getElementById(SCRIPT_ID);

    if (existing) {
      existing.addEventListener('load', () => resolve(), { once: true });
      existing.addEventListener('error', () => reject(new Error('hCaptcha failed to load')), { once: true });
      return;
    }

    const script = document.createElement('script');
    script.id = SCRIPT_ID;
    // Explicit render: nothing is drawn until render() is called, so the widget stays out of the
    // page until a form actually needs one.
    script.src = SCRIPT_SRC;
    script.async = true;
    script.defer = true;
    script.addEventListener('load', () => resolve(), { once: true });
    script.addEventListener('error', () => reject(new Error('hCaptcha failed to load')), { once: true });
    document.head.appendChild(script);
  });

  // A failed load must not poison every later attempt, so the shared promise is released on
  // failure and the next caller tries again.
  loading.catch(() => {
    loading = null;
  });

  return loading;
}

/**
 * Mints an hCaptcha token at submit time.
 *
 * The widget is invisible: no checkbox sits in the form, and hCaptcha interrupts with a challenge
 * only when it is unsure about the visitor. Tokens are single use and short lived, so one is
 * requested per submission rather than held from page load — a token minted when the page opened
 * has usually expired by the time a careful person finishes typing.
 *
 * @param siteKey The public site key. The secret half belongs on the server and nowhere else.
 * @returns A function that resolves to a token, or to null when one cannot be obtained.
 */
export function useHCaptcha(siteKey: string): HCaptchaExecutor {
  const container = useRef<HTMLDivElement | null>(null);
  const widgetId = useRef<string | null>(null);

  useEffect(() => {
    // Warmed on mount so submitting is not also waiting on a network round trip.
    void loadScript().catch(() => undefined);

    const host = document.createElement('div');
    host.style.display = 'none';
    host.setAttribute('aria-hidden', 'true');
    document.body.appendChild(host);
    container.current = host;

    return () => {
      const api: HCaptchaApi | undefined = window.hcaptcha;

      if (api && widgetId.current !== null) {
        // Without this the widget's own listeners outlive the component.
        try {
          api.remove(widgetId.current);
        } catch {
          // Already gone. Nothing to release.
        }
      }

      host.remove();
      container.current = null;
      widgetId.current = null;
    };
  }, [siteKey]);

  return useCallback(async () => {
    try {
      await loadScript();

      const api = window.hcaptcha;
      if (!api || !container.current) return null;

      widgetId.current ??= api.render(container.current, {
        sitekey: siteKey,
        size: 'invisible',
      });

      const { response } = await api.execute(widgetId.current, { async: true });

      // A token is spent once the server verifies it, so the widget is cleared for the next
      // attempt. Without this a second submission reuses a token the server will reject.
      api.reset(widgetId.current);

      return response || null;
    } catch {
      return null;
    }
  }, [siteKey]);
}
