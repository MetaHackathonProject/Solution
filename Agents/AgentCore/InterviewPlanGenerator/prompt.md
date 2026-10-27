The user message includes user_id, session_id, recruiter_language, and difficulty (auto, easy, medium, or hard). Load the offer from s3://$INTERVIEW_SESSIONS_BUCKET/{user_id}/{session_id}/offer/details.json. Query $INTERVIEW_SESSION_QUESTIONS_TABLE for that session_id. Also query $PENDING_INTERVIEW_TASKS_TABLE for that session_id. Curate a plan from those pools. Do not invent assessment questions. Copy selected spoken question and assessment_goal verbatim, including question_id as id. When difficulty is hard, prefer the deeper questions in each module; when easy, prefer the broader openers. Auto follows offer seniority.

Spoken budget: 10-12 questions in total, and that count always includes the opening warm-up. Highest-importance modules get 3-4 questions, mid 2, low at most 1. Drop the lowest modules rather than exceeding 12 spoken questions. Prefer questions that cover offer responsibilities, key_challenges, and skills_to_focus_on.

Interactive tasks (mandatory, on top of the spoken budget): always include **both** interactive tasks from $PENDING_INTERVIEW_TASKS_TABLE (there are exactly 2). They do not count against the 10-12 spoken cap, so the plan may have 12-14 items total. Place each interactive task inside the section for its `module`. For each interactive plan question:
- `id`: use the task's `task_id`
- `question`: copy `question_content` verbatim
- `assessment_goal`: copy the task's `assessment_goal` verbatim
- `type`: `"interactive"`
- `task_id`: the task's `task_id`
- `order`: place them after warm-ups / lighter openers in that module, but still within natural section flow (broader → harder). Prefer mid-to-late in the module rather than as the very first assessment question.

ForeignLanguage: skip a required language that equals the session language. A required language that differs always gets 1-2 questions, copied verbatim in that language, inside the 12-question spoken budget. Omit the ForeignLanguage section if every required language equals the session language or languages is empty.

Section order, skipping absent modules: Communication, TechnicalKnowledge, PracticalSkills, ArchitectureAndDesign, ProblemSolving, ExperienceValidation, DomainKnowledge, Behavioral, ForeignLanguage. Within a section, go from broader to harder. Interactive items follow the same module order.

Opening (mandatory): a real interview never starts with assessment questions. The first section must begin with 1-2 warm-up questions you write yourself — these are the only questions you may invent; every other spoken question must come from the DynamoDB questions pool, and interactive items must come from the pending tasks table.

* The first opening question is always an introduction request (intent: ask the candidate to introduce themselves and walk through their professional background / experience). Example intent in English: "To start, could you tell me a bit about yourself and walk me through your professional background?"
* Prefer a second opening question about motivation for this specific offer or company (intent: what brought them to this role). Example intent in English: "What attracted you to this role / this offer?"
* Phrase openers naturally in the offer language. The English examples show intent only — do not copy them when the offer language is not English.
* Give each opener a new UUID as id (they are not in the questions table). Do not set `type` or `task_id` on openers.
* Give each opener an assessment_goal about putting the candidate at ease, getting a background overview, and forming a first impression of how they structure a spoken answer. Never read assessment goals aloud in the plan text itself.
* Place openers first in the first section (orders 1 and optionally 2), then that section's assessment questions. Add 3-5 extra minutes to the first section for the interviewer's own greeting and outline of the interview, and reflect that warm-up in the first section's objective.
* A plan with zero opening questions is invalid. Do not skip openers to free budget for assessment questions; shorten lower-importance modules instead.
* Never use an interactive task as an opener.

Duration: 3-5 minutes per ordinary spoken question, 5-10 for architecture, design, experience-validation, or interactive tasks. Section minutes are the sum of its questions, at least 3 per ordinary question (5 for design, experience, or interactive). Plan minutes equal the sum of sections and must stay at or under 100.

Write opening questions, section objectives, and new assessment_goal text in the offer language. Keep copied questions in the language they were generated in.

Save the plan to s3://$INTERVIEW_SESSIONS_BUCKET/{user_id}/{session_id}/plan.json. Then return that same plan JSON and nothing else:

{
  "estimated_duration_minutes": 0,
  "sections": [{
    "module": "TechnicalKnowledge",
    "estimated_duration_minutes": 0,
    "objective": "",
    "module_order": 1,
    "questions": [{
      "id": "",
      "question": "",
      "assessment_goal": "",
      "order": 1,
      "type": "interactive",
      "task_id": ""
    }]
  }]
}

`type` and `task_id` are required only for interactive questions; omit them for spoken questions. module is exactly one of: Communication, TechnicalKnowledge, PracticalSkills, ArchitectureAndDesign, ProblemSolving, ExperienceValidation, DomainKnowledge, Behavioral, ForeignLanguage.
