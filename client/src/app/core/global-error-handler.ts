import { ErrorHandler, Injectable, NgZone, inject } from '@angular/core';

/**
 * Captures uncaught render/runtime errors from Angular so they don't white-screen the SPA.
 * Errors are logged to the console (preserving stack traces for dev tools) and surfaced via a
 * tiny floating banner that the user can dismiss. Network/HTTP errors continue to be handled
 * by per-feature observables; this is the last line of defense.
 */
@Injectable({ providedIn: 'root' })
export class GlobalErrorHandler implements ErrorHandler {
  private readonly zone = inject(NgZone);
  private bannerElement: HTMLDivElement | null = null;

  handleError(error: unknown): void {
    const message = this.toMessage(error);
    console.error('[FplLiveRank] Uncaught error:', error);

    if (typeof document === 'undefined') return;

    this.zone.runOutsideAngular(() => this.showBanner(message));
  }

  private toMessage(error: unknown): string {
    if (error instanceof Error) return error.message || error.name;
    if (typeof error === 'string') return error;
    try {
      return JSON.stringify(error);
    } catch {
      return 'Unexpected error.';
    }
  }

  private showBanner(message: string): void {
    if (!this.bannerElement) {
      this.bannerElement = document.createElement('div');
      this.bannerElement.className =
        'fixed bottom-4 right-4 z-50 max-w-md rounded-lg bg-rose-900/95 p-4 text-sm text-rose-100 shadow-lg ring-1 ring-rose-700';

      const text = document.createElement('p');
      text.className = 'font-medium';
      text.textContent = 'Something went wrong rendering the page.';
      this.bannerElement.appendChild(text);

      const detail = document.createElement('p');
      detail.className = 'mt-1 text-xs text-rose-200/80';
      detail.dataset['role'] = 'detail';
      this.bannerElement.appendChild(detail);

      const dismiss = document.createElement('button');
      dismiss.type = 'button';
      dismiss.className = 'mt-2 text-xs font-semibold text-rose-100 underline hover:text-white';
      dismiss.textContent = 'Dismiss';
      dismiss.addEventListener('click', () => {
        this.bannerElement?.remove();
        this.bannerElement = null;
      });
      this.bannerElement.appendChild(dismiss);

      document.body.appendChild(this.bannerElement);
    }

    const detail = this.bannerElement.querySelector<HTMLParagraphElement>('[data-role="detail"]');
    if (detail) detail.textContent = message;
  }
}
