/**
 * The slice of hCaptcha's global object this package uses.
 *
 * Declared here rather than taken from a types package: the surface is three methods, and a
 * dependency whose only job is to describe three methods is a dependency to keep updated forever.
 */
export type HCaptchaApi = {
  render: (container: HTMLElement, options: HCaptchaRenderOptions) => string;
  execute: (widgetId: string, options: { async: true }) => Promise<{ response: string }>;
  reset: (widgetId: string) => void;
  remove: (widgetId: string) => void;
};

export type HCaptchaRenderOptions = {
  sitekey: string;
  size?: 'normal' | 'compact' | 'invisible';
  theme?: 'light' | 'dark';
};

declare global {
  interface Window {
    hcaptcha?: HCaptchaApi;
  }
}

/**
 * Returns a token, or null when one cannot be obtained.
 *
 * Null covers every reason a visitor might not produce one: a blocked script, an extension, an
 * offline machine, or a challenge they closed. The caller decides what to say about it, because
 * this package has no idea what the surrounding form looks like.
 */
export type HCaptchaExecutor = () => Promise<string | null>;
