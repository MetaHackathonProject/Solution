The user message includes user_id, session_id, recruiter_language, and difficulty (auto, easy, medium, or hard). Load the offer from s3://$INTERVIEW_SESSIONS_BUCKET/{user_id}/{session_id}/offer/details.json. Do not invent skills or technologies that are not in that file.

Generate interview questions for every module in recommended_interview_modules and must_cover_modules. Skip a module that is not in this list: Communication, TechnicalKnowledge, PracticalSkills, ArchitectureAndDesign, ProblemSolving, ExperienceValidation, DomainKnowledge, Behavioral, ForeignLanguage.

Per module: 8-10 questions when that module is core to the role, 5-7 when it is relevant, 3-4 when it is peripheral. Cover skills_to_focus_on first. Questions are open-ended, spoken, one topic each, no overlap. Write them in the offer language. Keep technology names as they are.

If difficulty is auto, pick Easy for entry/junior, Medium for mid, Hard for senior, lead, principal, or high required experience. Then apply that level: Easy is core concepts and common usage; Medium is trade-offs and why rather than what; Hard is edge cases, failure modes, and scale. Hard for a junior role still stays in junior scope; Easy for a principal role is still substantive.

Write each spoken question as its own DynamoDB item in $INTERVIEW_SESSION_QUESTIONS_TABLE. Every spoken item MUST include all of these attributes (do not omit helpers):
- session_id: the session id from the user message
- question_id: a new UUID
- module: the module name
- question: the question text
- assessment_goal: one or two sentences naming the competency and the evidence the interviewer should listen for
- suggested_answer: **required** string — a strong sample spoken answer (about 4–8 sentences) the candidate can study as a helper; write it in `recruiter_language` from the user message (the live interview language). Do not invent employer facts or stack not in the offer. Never leave this blank.
- hints: **required** DynamoDB **string list** (type L of S) of 3–5 short bullets (also in `recruiter_language`) — talking points / structure cues, not a full script. Example: list attribute with values like `Lead with the outcome`, `Name one trade-off`, `Close with what you would measure`. Do not store hints as a single JSON string.

Also always create exactly **2 interactive tasks** in $PENDING_INTERVIEW_TASKS_TABLE. They are a type of question under an existing module — not a new module name. Pick modules that matter for the offer (prefer PracticalSkills, ArchitectureAndDesign, ProblemSolving, TechnicalKnowledge, DomainKnowledge).

When the offer involves programming or software engineering, **at least one** interactive task (both if the role is strongly coding-focused) must be a **LeetCode-style algorithmic coding problem** with `expected_input` = `code`. Do **not** use CRUD apps, “build an API endpoint”, framework scaffolding, or copy-paste boilerplate as coding tasks.

Coding / LeetCode tasks (`expected_input` = `code`):
- Self-contained algorithmic problem: arrays, strings, hash maps, trees, graphs, recursion, DP, two pointers, sliding window, sorting/searching, etc., chosen to fit skills_to_focus_on and seniority.
- Difficulty mapping: Easy → straightforward single-idea problem; Medium → requires combining patterns or careful edge cases; Hard → harder algorithms / tighter constraints (still scoped to the role’s level).
- Prefer the languages/stacks named in the offer for any signature or language hint, but keep the problem language-agnostic enough that the candidate can solve it in their preferred language unless the offer demands one.
- `question_content` (spoken): one or two sentences naming the challenge (e.g. ask them to solve the on-screen algorithmic problem about X). Do not read constraints or examples aloud.
- `question_content_formatted` (Markdown on screen) must include, in this order:
  1. **Title**
  2. **Problem** — clear statement of what to compute
  3. **Function signature / I/O** — inputs, outputs, and a suggested signature or clear I/O format
  4. **Constraints** — sizes, value ranges, time/space expectations if relevant
  5. **Examples** — at least two, with input, output, and a one-line explanation
  6. **Notes** — optional edge cases the solution should handle
- Assess algorithm choice, correctness, complexity awareness, and edge cases — not UI or framework knowledge.

Other interactive tasks (use when a second non-coding exercise fits better): architecture/system design drawing → `diagram`; DB/ER modeling → `schema`; short written design → `text` or `free_string`.

Each interactive task item:
- session_id: the session id from the user message
- task_id: a new UUID
- module: one of the allowed module names above
- question_content: short spoken prompt the voice interviewer will read aloud (one or two sentences; do not dump the full exercise)
- question_content_formatted: full exercise in Markdown shown on screen
- expected_input: exactly one of `code`, `diagram`, `schema`, `text`, `free_string`
- status: always `NotAsked`
- assessment_goal: what evidence a strong submission should show

Then return JSON and nothing else:

{ "session_id": "", "modules": [{ "module": "", "count": 0 }], "interactive_tasks": 2, "total": 0 }
