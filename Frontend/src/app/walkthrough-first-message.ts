import type { Language } from '@elevenlabs/client';

const WALKTHROUGH_BY_LANGUAGE: Partial<Record<Language, string>> = {
  en: 'Thank you for submitting your solution. Can you please walk me through it?',
  pl: 'Dziękuję za przesłanie rozwiązania. Czy możesz mi je proszę wyjaśnić?',
  de: 'Danke, dass Sie Ihre Lösung eingereicht haben. Können Sie sie mir bitte erklären?',
  fr: 'Merci d’avoir soumis votre solution. Pouvez-vous me l’expliquer, s’il vous plaît ?',
  es: 'Gracias por enviar tu solución. ¿Puedes explicármela, por favor?',
  it: 'Grazie per aver inviato la tua soluzione. Puoi spiegarmela, per favore?',
  pt: 'Obrigado por submeter a sua solução. Pode explicar-ma, por favor?',
  nl: 'Bedankt voor het indienen van je oplossing. Kun je die even voor me toelichten?',
  cs: 'Děkuji za odevzdání řešení. Můžete mi ho prosím projít?',
  sk: 'Ďakujem za odovzdanie riešenia. Môžete mi ho prosím prejsť?',
  uk: 'Дякую, що надіслали рішення. Чи можете, будь ласка, коротко його пояснити?',
  sv: 'Tack för att du skickade in din lösning. Kan du gå igenom den åt mig?',
  no: 'Takk for at du sendte inn løsningen. Kan du gå gjennom den for meg?',
  da: 'Tak fordi du indsendte din løsning. Kan du gennemgå den for mig?',
  fi: 'Kiitos ratkaisun lähettämisestä. Voisitko käydä sen läpi?',
  hu: 'Köszönöm, hogy beküldted a megoldásod. Megtennéd, hogy végigvezetnél rajta?',
  ro: 'Mulțumesc că ai trimis soluția. Poți să mi-o explici, te rog?',
  el: 'Ευχαριστώ που υπέβαλες τη λύση σου. Μπορείς παρακαλώ να μου την εξηγήσεις;',
  tr: 'Çözümünü gönderdiğin için teşekkürler. Lütfen bana anlatır mısın?',
  ja: '解答を提出してくれてありがとう。内容を説明してもらえますか？',
  ko: '솔루션을 제출해 주셔서 감사합니다. 내용을 설명해 주시겠어요?',
  zh: '感谢提交你的解答。能请你讲解一下吗？',
  ar: 'شكرًا لتقديم حلولك. هل يمكنك شرحها لي من فضلك؟',
  hi: 'अपना समाधान जमा करने के लिए धन्यवाद। क्या आप कृपया मुझे समझा सकते हैं?',
};

export function walkthroughFirstMessage(language: Language | undefined): string {
  if (!language) {
    return WALKTHROUGH_BY_LANGUAGE.en!;
  }

  return WALKTHROUGH_BY_LANGUAGE[language] ?? WALKTHROUGH_BY_LANGUAGE.en!;
}
