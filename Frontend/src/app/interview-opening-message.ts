import type { Language } from '@elevenlabs/client';
import { resumeInterviewFirstMessage } from './resume-interview-first-message';

function withRole(template: string, jobTitle: string, company: string): string {
  return template
    .replaceAll('{{job_title}}', jobTitle || 'this')
    .replaceAll('{{company}}', company || 'the company');
}

const INTRO_BY_LANGUAGE: Partial<Record<Language, string>> = {
  en: "Hi, my name is John and I will lead today's interview for the {{job_title}} role at {{company}}. Ready to begin?",
  pl: 'Cześć, nazywam się John i poprowadzę dzisiejszą rozmowę kwalifikacyjną na stanowisko {{job_title}} w {{company}}. Gotowy, żeby zacząć?',
  de: 'Hallo, mein Name ist John und ich werde das heutige Vorstellungsgespräch für die Position {{job_title}} bei {{company}} führen. Bereit, anzufangen?',
  fr: "Bonjour, je m'appelle John et je dirigerai l'entretien d'aujourd'hui pour le poste de {{job_title}} chez {{company}}. Prêt(e) à commencer ?",
  es: 'Hola, me llamo John y dirigiré la entrevista de hoy para el puesto de {{job_title}} en {{company}}. ¿Listo para comenzar?',
  it: "Ciao, mi chiamo John e condurrò il colloquio di oggi per il ruolo di {{job_title}} presso {{company}}. Pronto per iniziare?",
  pt: 'Olá, o meu nome é John e vou conduzir a entrevista de hoje para a função de {{job_title}} na {{company}}. Pronto para começar?',
  nl: 'Hallo, mijn naam is John en ik leid het interview van vandaag voor de rol van {{job_title}} bij {{company}}. Klaar om te beginnen?',
  cs: 'Ahoj, jmenuji se John a dnes povedu pohovor na pozici {{job_title}} ve společnosti {{company}}. Jste připraveni začít?',
  sk: 'Ahoj, volám sa John a dnes povediem pohovor na pozíciu {{job_title}} v spoločnosti {{company}}. Ste pripravení začať?',
  uk: 'Привіт, мене звати Джон, і сьогодні я проведу співбесіду на посаду {{job_title}} у {{company}}. Готові почати?',
  sv: 'Hej, jag heter John och jag leder dagens intervju för rollen som {{job_title}} på {{company}}. Redo att börja?',
  no: 'Hei, jeg heter John og jeg leder dagens intervju for rollen som {{job_title}} hos {{company}}. Klar til å starte?',
  da: 'Hej, jeg hedder John, og jeg vil lede dagens interview til stillingen som {{job_title}} hos {{company}}. Er du klar til at begynde?',
  fi: 'Hei, nimeni on John, ja johdan tänään {{company}}-yrityksen {{job_title}}-tehtävän haastattelua. Oletko valmis aloittamaan?',
  hu: 'Szia, a nevem John, és én vezetem a mai interjút a {{job_title}} pozícióra a {{company}} cégnél. Készen állsz a kezdésre?',
  ro: 'Bună, mă numesc John și voi conduce interviul de azi pentru rolul de {{job_title}} la {{company}}. Ești gata să începem?',
  el: 'Γεια σας, με λένε John και θα διευθύνω τη σημερινή συνέντευξη για τον ρόλο {{job_title}} στην {{company}}. Έτοιμοι να ξεκινήσουμε;',
  tr: 'Merhaba, adım John ve bugün {{company}} bünyesindeki {{job_title}} rolü için mülakatı ben yöneteceğim. Başlamaya hazır mısınız?',
  ja: 'こんにちは、ジョンです。本日は{{company}}の{{job_title}}職の面接を担当します。始める準備はできていますか？',
  ko: '안녕하세요, 저는 John입니다. 오늘 {{company}}의 {{job_title}} 역할 면접을 진행하겠습니다. 시작할 준비되셨나요?',
  zh: '你好，我叫约翰，今天将由我主持针对 {{company}} 的 {{job_title}} 职位的面试。准备好开始了吗？',
  ar: 'مرحبًا، اسمي جون وسأجري مقابلة اليوم لشغل وظيفة {{job_title}} في {{company}}. هل أنت مستعد للبدء؟',
  hi: 'नमस्ते, मेरा नाम जॉन है और मैं आज {{company}} में {{job_title}} भूमिका के लिए साक्षात्कार का नेतृत्व करूँगा। शुरू करने के लिए तैयार हैं?',
};

export function interviewOpeningMessage(options: {
  isResume: boolean;
  language?: Language;
  jobTitle?: string | null;
  company?: string | null;
}): string {
  if (options.isResume) {
    return resumeInterviewFirstMessage(options.language);
  }

  const template =
    (options.language && INTRO_BY_LANGUAGE[options.language]) || INTRO_BY_LANGUAGE.en!;

  return withRole(template, options.jobTitle ?? '', options.company ?? '');
}
