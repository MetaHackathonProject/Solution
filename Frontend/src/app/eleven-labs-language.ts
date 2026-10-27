import type { Language } from '@elevenlabs/client';

const NAME_TO_CODE: Record<string, Language> = {
  english: 'en',
  polish: 'pl',
  german: 'de',
  french: 'fr',
  spanish: 'es',
  italian: 'it',
  portuguese: 'pt',
  dutch: 'nl',
  czech: 'cs',
  slovak: 'sk',
  ukrainian: 'uk',
  russian: 'ru',
  swedish: 'sv',
  norwegian: 'no',
  danish: 'da',
  finnish: 'fi',
  hungarian: 'hu',
  romanian: 'ro',
  greek: 'el',
  turkish: 'tr',
  japanese: 'ja',
  korean: 'ko',
  chinese: 'zh',
  arabic: 'ar',
  hindi: 'hi',
};

export function toElevenLabsLanguage(value: string | null | undefined): Language | undefined {
  if (!value) {
    return undefined;
  }

  const normalized = value.trim().toLowerCase().replace(/[_-]+/g, ' ');
  if (!normalized) {
    return undefined;
  }

  return NAME_TO_CODE[normalized] ?? NAME_TO_CODE[normalized.split(' ')[0]];
}
