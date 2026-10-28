import type { Language } from '@elevenlabs/client';

const CONTINUE_BY_LANGUAGE: Partial<Record<Language, string>> = {
  en: "Thanks for walking me through that. Let's continue with the interview.",
  pl: 'Dziękuję za omówienie rozwiązania. Kontynuujmy rozmowę kwalifikacyjną.',
  de: 'Danke für die Erklärung. Lassen Sie uns das Interview fortsetzen.',
  fr: 'Merci pour ces explications. Continuons l’entretien.',
  es: 'Gracias por la explicación. Continuemos con la entrevista.',
  it: 'Grazie per la spiegazione. Continuiamo il colloquio.',
  pt: 'Obrigado pela explicação. Vamos continuar a entrevista.',
  nl: 'Bedankt voor de toelichting. Laten we het interview voortzetten.',
  cs: 'Děkuji za vysvětlení. Pokračujme v pohovoru.',
  sk: 'Ďakujem za vysvetlenie. Pokračujme v pohovore.',
  uk: 'Дякую за пояснення. Продовжимо співбесіду.',
  sv: 'Tack för genomgången. Låt oss fortsätta intervjun.',
  no: 'Takk for gjennomgangen. La oss fortsette intervjuet.',
  da: 'Tak for gennemgangen. Lad os fortsætte interviewet.',
  fi: 'Kiitos läpikäynnistä. Jatketaan haastattelua.',
  hu: 'Köszönöm a magyarázatot. Folytassuk az interjút.',
  ro: 'Mulțumesc pentru explicație. Să continuăm interviul.',
  el: 'Ευχαριστώ για την εξήγηση. Ας συνεχίσουμε τη συνέντευξη.',
  tr: 'Açıklaman için teşekkürler. Mülakata devam edelim.',
  ja: '説明ありがとう。面接を続けましょう。',
  ko: '설명해 주셔서 감사합니다. 면접을 이어가겠습니다.',
  zh: '谢谢你的讲解。我们继续面试吧。',
  ar: 'شكرًا على الشرح. لنتابع المقابلة.',
  hi: 'समझाने के लिए धन्यवाद। चलिए साक्षात्कार जारी रखते हैं।',
};

export function continueInterviewFirstMessage(language: Language | undefined): string {
  if (!language) {
    return CONTINUE_BY_LANGUAGE.en!;
  }

  return CONTINUE_BY_LANGUAGE[language] ?? CONTINUE_BY_LANGUAGE.en!;
}
