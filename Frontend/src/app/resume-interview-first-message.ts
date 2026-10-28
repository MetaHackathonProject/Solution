import type { Language } from '@elevenlabs/client';

const RESUME_BY_LANGUAGE: Partial<Record<Language, string>> = {
  en: "Welcome back. Let's continue our interview where we left off.",
  pl: 'Witaj ponownie. Kontynuujmy rozmowę kwalifikacyjną od miejsca, w którym skończyliśmy.',
  de: 'Willkommen zurück. Lassen Sie uns das Interview dort fortsetzen, wo wir aufgehört haben.',
  fr: 'Bon retour. Continuons l’entretien là où nous nous sommes arrêtés.',
  es: 'Bienvenido de nuevo. Continuemos la entrevista donde la dejamos.',
  it: 'Bentornato. Continuiamo il colloquio da dove ci eravamo interrotti.',
  pt: 'Bem-vindo de volta. Vamos continuar a entrevista de onde paramos.',
  nl: 'Welkom terug. Laten we het interview voortzetten waar we gebleven waren.',
  cs: 'Vítejte zpět. Pokračujme v pohovoru tam, kde jsme skončili.',
  sk: 'Vitajte späť. Pokračujme v pohovore tam, kde sme skončili.',
  uk: 'З поверненням. Продовжимо співбесіду з того місця, де зупинилися.',
  sv: 'Välkommen tillbaka. Låt oss fortsätta intervjun där vi slutade.',
  no: 'Velkommen tilbake. La oss fortsette intervjuet der vi slapp.',
  da: 'Velkommen tilbage. Lad os fortsætte interviewet, hvor vi slap.',
  fi: 'Tervetuloa takaisin. Jatketaan haastattelua siitä, mihin jäimme.',
  hu: 'Üdv újra. Folytassuk az interjút onnan, ahol abbahagytuk.',
  ro: 'Bine ai revenit. Să continuăm interviul de unde am rămas.',
  el: 'Καλώς ήρθες ξανά. Ας συνεχίσουμε τη συνέντευξη από εκεί που σταματήσαμε.',
  tr: 'Tekrar hoş geldiniz. Mülakata kaldığımız yerden devam edelim.',
  ja: 'おかえりなさい。中断したところから面接を続けましょう。',
  ko: '다시 오신 것을 환영합니다. 중단했던 부분부터 면접을 이어가겠습니다.',
  zh: '欢迎回来。我们从上次停下的地方继续面试吧。',
  ar: 'مرحبًا بعودتك. لنتابع المقابلة من حيث توقفنا.',
  hi: 'वापसी पर स्वागत है। चलिए साक्षात्कार वहीं से जारी रखते हैं जहाँ हमने छोड़ा था।',
};

export function resumeInterviewFirstMessage(language: Language | undefined): string {
  if (!language) {
    return RESUME_BY_LANGUAGE.en!;
  }

  return RESUME_BY_LANGUAGE[language] ?? RESUME_BY_LANGUAGE.en!;
}
