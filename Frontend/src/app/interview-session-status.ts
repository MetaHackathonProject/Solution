export type InterviewSessionStatus =
  | 'STARTED'
  | 'READING_OFFER'
  | 'GENERATING_QUESTIONS'
  | 'GENERATING_PLAN'
  | 'SUCCESS'
  | 'IN_PROGRESS'
  | 'COMPLETED'
  | 'FAILED';

export interface StatusStep {
  id: InterviewSessionStatus;
  label: string;
  detail: string;
}

export const PREPARING_STEPS: StatusStep[] = [
  {
    id: 'STARTED',
    label: 'Starting',
    detail: 'Queuing the interview preparation pipeline.',
  },
  {
    id: 'READING_OFFER',
    label: 'Reading offer',
    detail: 'Opening the job posting and extracting the role profile.',
  },
  {
    id: 'GENERATING_QUESTIONS',
    label: 'Generating questions',
    detail: 'Building a question bank from the offer modules.',
  },
  {
    id: 'GENERATING_PLAN',
    label: 'Generating plan',
    detail: 'Curating the spoken interview plan.',
  },
];

export function isTerminalStatus(status: string): boolean {
  return (
    status === 'SUCCESS' ||
    status === 'FAILED' ||
    status === 'IN_PROGRESS' ||
    status === 'COMPLETED'
  );
}

export function isPreparingStatus(status: string): boolean {
  return (
    status === 'STARTED' ||
    status === 'READING_OFFER' ||
    status === 'GENERATING_QUESTIONS' ||
    status === 'GENERATING_PLAN'
  );
}

export function statusLabel(status: string): string {
  switch (status) {
    case 'STARTED':
      return 'Starting';
    case 'READING_OFFER':
      return 'Reading offer';
    case 'GENERATING_QUESTIONS':
      return 'Generating questions';
    case 'GENERATING_PLAN':
      return 'Generating plan';
    case 'SUCCESS':
      return 'Ready';
    case 'IN_PROGRESS':
      return 'In progress';
    case 'COMPLETED':
      return 'Completed';
    case 'FAILED':
      return 'Failed';
    default:
      return status;
  }
}

export function statusDetail(status: string): string {
  switch (status) {
    case 'STARTED':
      return 'Queuing the interview preparation pipeline.';
    case 'READING_OFFER':
      return 'Opening the job posting and extracting the role profile.';
    case 'GENERATING_QUESTIONS':
      return 'Building a question bank from the offer modules.';
    case 'GENERATING_PLAN':
      return 'Curating the spoken interview plan.';
    case 'SUCCESS':
      return 'The interview plan is ready.';
    case 'IN_PROGRESS':
      return 'The live interview is in progress. You can continue from where you left off if the queue still has questions.';
    case 'COMPLETED':
      return 'This interview is finished. The question queue was exhausted.';
    case 'FAILED':
      return 'Preparation failed. Create a new session to try again.';
    default:
      return 'Checking preparation status…';
  }
}

export function statusTone(status: string): 'preparing' | 'ready' | 'failed' | 'active' | 'done' {
  if (status === 'SUCCESS') {
    return 'ready';
  }

  if (status === 'IN_PROGRESS') {
    return 'active';
  }

  if (status === 'COMPLETED') {
    return 'done';
  }

  if (status === 'FAILED') {
    return 'failed';
  }

  return 'preparing';
}

export function stepState(
  stepId: InterviewSessionStatus,
  currentStatus: string,
): 'done' | 'active' | 'pending' {
  if (currentStatus === 'SUCCESS') {
    return 'done';
  }

  const order = PREPARING_STEPS.map((step) => step.id);
  const currentIndex = order.indexOf(currentStatus as InterviewSessionStatus);
  const stepIndex = order.indexOf(stepId);

  if (currentIndex < 0) {
    return stepIndex === 0 ? 'active' : 'pending';
  }

  if (stepIndex < currentIndex) {
    return 'done';
  }

  if (stepIndex === currentIndex) {
    return 'active';
  }

  return 'pending';
}
