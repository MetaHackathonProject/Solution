# Agent instructions

This repo is a mock-interview product: extract a job offer, generate questions, curate a spoken plan, then run the interview. AWS is **eu-central-1**. Do not invent credentials, API keys, or offer facts that are not in the posting.

## Layout

- `Frontend/` — Angular 20, Cognito OIDC (`angular-auth-oidc-client`). Env via `Frontend/scripts/load-env.mjs` (`COGNITO_AUTHORITY`, `COGNITO_CLIENT_ID`).
- `Backend/` — .NET 10, MediatR. `Hackathon.Api` → `Hackathon.Application` → `Hackathon.Domain`. `Hackathon.Infrastructure` is AWS adapters. Lambda is an **Event Function** (`Amazon.Lambda.AspNetCoreServer`), not a Web Function.
- `Infrastructure/` — Terraform. Agents are not defined here; Terraform reads YAML.
- `Agents/AgentCore/<Name>/` — one harness per folder. This is the only AgentCore source of truth.
- `Agents/ElevenLabsAgent/` — main voice interviewer export. Do not Terraform it.
- `Agents/ElevenLabsAgent/WalkthroughAgent/` — post-submit solution walkthrough voice agent (no interview-questions MCP). Do not Terraform it; set `eleven_labs_walkthrough_agent_id`.

## AgentCore

Terraform: `fileset(Agents/AgentCore, "*/agent.yaml")`. **Every** sibling folder becomes a harness. Do not leave leftover extractors under `Agents/AgentCore/`.

Required files:

```
Agents/AgentCore/<Name>/agent.yaml
Agents/AgentCore/<Name>/prompt.md
```

`agent.yaml` shape:

```yaml
name: snake_case_name          # letter first, [A-Za-z0-9_], max 40
timeout_seconds: 600
max_iterations: 20
model:
  provider: litellm            # required
  model_id: openai/muse-spark-1.3
  api_base: https://api.meta.ai/v1
  api_key_arn: arn:aws:bedrock-agentcore:...  # existing Identity key, copy from OfferExtractor
tools: []
```

Browser tool (offer extractor only): pin both name and ARN or the browser detaches on apply.

```yaml
tools:
  - type: agentcore_browser
    name: aws_browser_v1
    browser_arn: arn:aws:bedrock-agentcore:eu-central-1:aws:browser/aws.browser.v1
```

Harness env already set in Terraform: `INTERVIEW_SESSIONS_BUCKET`, `INTERVIEW_SESSION_QUESTIONS_TABLE`, `PENDING_INTERVIEW_TASKS_TABLE`. IAM covers S3 get/put on the sessions bucket, DynamoDB on the questions + pending-tasks tables, memory `ListEvents`, `GetResourceApiKey` on the token vault, workload identity, and the key ARN.

Prompts must name `$INTERVIEW_SESSIONS_BUCKET`, `$INTERVIEW_SESSION_QUESTIONS_TABLE`, and `$PENDING_INTERVIEW_TASKS_TABLE` where used. User message always includes `user_id` and `session_id`.

Pipeline (do not collapse into one agent):

1. `offer_extraction` — browser, write `s3://$INTERVIEW_SESSIONS_BUCKET/{user_id}/{session_id}/offer/details.json`
2. `question_generation` — read details, write DynamoDB spoken question items (`session_id`, `question_id` UUID, `module`, `question`, `assessment_goal`, `suggested_answer`, `hints` string list; helpers in `recruiter_language`) **and** exactly 2 interactive tasks to `$PENDING_INTERVIEW_TASKS_TABLE` (`session_id`, `task_id`, `module`, `question_content`, `question_content_formatted`, `expected_input`, `status=NotAsked`, `assessment_goal`)
3. `interview_plan_generation` — read details + questions + pending tasks, write `.../{user_id}/{session_id}/plan.json` only (always include both interactive tasks with `type: "interactive"` + `task_id`, on top of the 10–12 spoken cap)

Separate from the prep pipeline (started on interactive answer submit when the answer is visual):

4. `Hackathon.DescribeSolutionVision` Lambda — reads the S3 answer, calls Meta `muse-spark-1.3` with a base64 data-URL image/PDF, writes `vision_review_status=Ready` + `vision_description` on the pending-task item (or `Failed`). Orchestrated by `solution_vision_review` Step Functions (`Pending` → `Running` → Lambda → catch `Failed`). Not an AgentCore harness (InvokeHarness cannot pass image content blocks).

Do **not** have agents write the progress table. That table is for live-interview runtime.

Orchestrate with Step Functions (`Infrastructure/step_functions.tf`), not a supervisor agent. After plan generation, Step Functions invokes `Hackathon.SeedInterviewProgress` to fill the progress table from `plan.json`, then sets `SUCCESS`. Statuses live on the sessions table attribute `status`: `STARTED`, `READING_OFFER`, `GENERATING_QUESTIONS`, `GENERATING_PLAN`, `SUCCESS`, `IN_PROGRESS`, `COMPLETED`, `FAILED`. The API persists the session then `states:StartExecution`. Do not have agents write status.

Live interview (API + frontend, not AgentCore):

1. `GET /api/voice-agent/signed-url?sessionId=&purpose=interview|walkthrough` — ElevenLabs ConvAI signed URL for the main interviewer or walkthrough agent; sets status `IN_PROGRESS` when session is `SUCCESS` or `IN_PROGRESS`.
2. `GET /api/interview-sessions/{sessionId}/voice-context` — job title/company/overview/language for ConvAI dynamic variables.
3. Frontend `/interviews/:sessionId/play` starts `@elevenlabs/client` with `sessionId`, `job_title`, `company`, `job_overview`, and `is_resume` (`true` when session was already `IN_PROGRESS`). Always passes `overrides.agent.firstMessage` (intro, resume, walkthrough, or continue-after-walkthrough); agent Security must allow First message overrides so language presets do not force the stock greeting.
4. Interactive pause: `start_interactive_task` opens the panel; after the spoken intro the agent calls `pause_for_interactive_task`, then the frontend **locally** ends the ConvAI session (no `end-conversation` API) so idle exercise time is not billed (90s fallback if the pause tool is never called). Stay paused until submit. Interview close: agent should call client tool `finish_interview` with the goodbye (frontend hangs up immediately; also auto-closes if an agent message looks like a goodbye).
5. After submit: for diagram/schema/image/PDF answers, API starts the `solution_vision_review` Step Functions machine (`Hackathon.DescribeSolutionVision` → Meta Muse Spark vision), which writes `vision_review_status` + `vision_description` on the pending-task Dynamo item. Frontend shows “Interviewer is now checking your solution…” and polls `GET .../interactive-task/{taskId}/vision-review` until `Ready`/`Failed` (or ~210s). Then it connects `purpose=walkthrough` with dynamic vars `submitted_solution` (code/text or the vision description, multi-file as `===== name =====`, capped ~10k chars), `task_prompt`, `expected_input`, `task_module`. Walkthrough agent has **no** interview-questions MCP; client tool `finish_solution_walkthrough` reconnects `purpose=interview` with a localized continue message.
6. `POST /api/voice-agent/end-conversation` — if the progress table has no remaining rows **and** no pending interactive tasks are `NotAsked`/`InProgress`, sets `COMPLETED`, sets `overview_status=GENERATING`, and starts the interview-overview Step Functions machine; otherwise leaves status alone. Only for real interview end, not interactive pause. Signed-url already persists each ConvAI `conversation_id` (purpose `interview`|`walkthrough`) to the conversations Dynamo table.
7. Interview overview (after `COMPLETED` only): `Hackathon.BuildInterviewOverview` merges all stored conversation transcripts (ElevenLabs GET `/v1/convai/conversations/{id}`), judges answered interactive tasks (code zip → Muse Wonder; visual → Muse Spark; text → Muse Spark), writes `{user_id}/{session_id}/overview.json` (+ transcript/judgment artifacts), and sets `overview_status` to `READY` or `ERROR`. Schema mirrors CV Maker (summary, per-question opinion/recommendations) plus overall `score` (1–10) and interactive `strengths`/`gaps`. `GET /api/interview-sessions/{sessionId}/overview` returns the JSON when ready; session responses expose `overviewStatus` (`NOT_AVAILABLE`|`GENERATING`|`READY`|`ERROR`).
8. `POST /api/interview-sessions/{sessionId}/retake` — same session id; resets pending interactive tasks to `NotAsked`, re-seeds progress from `plan.json`, sets status `SUCCESS`, resets `overview_status` to `NOT_AVAILABLE`, and deletes stored ConvAI conversation ids for that session so the next overview does not merge prior legs. Allowed from `COMPLETED`, `SUCCESS`, or `IN_PROGRESS` (UI exposes it for completed). Does not re-run offer extraction or question generation. Prior S3 interactive answers are left in place and overwritten on the next submit. Deletes `{user_id}/{session_id}/recording/` so camera segments from the previous attempt are not kept.
9. Interactive tasks: `GET /api/interview-sessions/{sessionId}/interactive-task` (active / resume), `GET .../interactive-task/{taskId}`, `POST .../answer` (multipart `text` or `file` → S3 `{user_id}/{session_id}/interactive-tasks/{task_id}/answer.*`). Code answers upload `answer.zip` (solution files + `manifest.json` with `language` / `entry` / `files`) for the review agent’s code interpreter. Voice agent client tool `start_interactive_task(taskId)` opens the play UI panel.
10. Candidate camera (optional): play page asks for camera independently of ConvAI so pause/walkthrough keep the tape if granted. Denied or missing camera still starts the interview (mic only). On interview end (or leaving play) with a tape, FE requests `GET /api/interview-sessions/{sessionId}/recording/upload-url` and PUTs the blob to a new segment `s3://.../{user_id}/{session_id}/recording/segments/{unix}-{guid}.webm` (or `.mp4`). Continue/resume appends another segment; retake deletes the `recording/` prefix. Bucket CORS allows PUT from the app origins.

## Interview domain

Modules (exact strings): `Communication`, `TechnicalKnowledge`, `PracticalSkills`, `ArchitectureAndDesign`, `ProblemSolving`, `ExperienceValidation`, `DomainKnowledge`, `Behavioral`, `ForeignLanguage`.

Interactive tasks are a **type** under those modules (not a new module). Table `pending-interview-tasks`: PK `session_id`, SK `task_id`. Status `NotAsked` → `InProgress` (MCP pop) → `Answered` (submit). `expected_input`: `code` | `diagram` | `schema` | `text` | `free_string`. Progress rows for interactive items include `task_id`; spoken content is `question_content` from the pending task.

Offer JSON: keep the offer language; no invented employers, skills, or years. `seniority` and `languages.proficiency` enums are in `OfferExtractor/prompt.md`. Module `score` is 1–10.

Plan: 10–12 spoken questions including mandatory 1–2 opening warm-ups (intro + optional motivation for the offer) that the plan agent may write; every other spoken question is copied verbatim with `assessment_goal`. Always also include both interactive tasks (`type: "interactive"`, `task_id`) on top of that spoken cap. Do not invent assessment questions.

## Backend

- Controllers stay thin; work goes in Application handlers.
- JWT from Cognito. Lambda reads SSM `/hackathon-solution/hackathon/JWT/*` and `Cors/AllowedOrigins`.
- New C# Lambdas: add a matrix row in `.github/workflows/backend.yml`. Function name pattern `{name}-hackathon` (API is `hackathon-solution-api-hackathon`).
- Handler for the API: `Hackathon.Api::Hackathon.Api.LambdaEntryPoint::FunctionHandlerAsync`.
- Progress seeding: `Hackathon.SeedInterviewProgress` reads `{user_id}/{session_id}/plan.json`, resolves interactive `task_id`s from the pending-tasks table, and replaces rows in the progress table (`order_key` = `{module_order:D4}#{order:D4}`, optional `task_id`). Step Functions invokes it after plan generation with `{ "user_id", "session_id" }`. Do not seed from AgentCore.
 - Live questions MCP: `Hackathon.Mcp` exposes `get_next_question(sessionId)` at `https://api.hackathon-solution.com/mcp/interview-questions/agent` (also `/mcp/interview-questions`; public, no JWT). Pops **one** ordered progress row at a time (spoken or interactive). If the next item is interactive, sets pending status `InProgress`. While any task is `InProgress`, every call re-returns that interactive item only (no further pops) until the candidate submits (`Answered`). Returns `type`/`taskId` on interactive items. When a spoken question is popped, MCP writes that single `question_id` to an `__active_helpers__` marker on the questions table so the play UI can poll `GET /api/interview-sessions/{sessionId}/question-helpers/active` for `suggested_answer` + `hints` (collapsed by default; only the current question). Interactive/in-progress clears the active marker. `GET .../question-helpers` lists all bank helpers for the session (overview). Warm-ups invented by the plan agent have no helpers. Old sessions without `suggested_answer` simply show none.
  - In ElevenLabs MCP server settings set **transport = `STREAMABLE_HTTP`** (default is `SSE`, which will show zero tools against this Lambda).
  - URL must be HTTPS and end with `/mcp/interview-questions` or `/mcp/interview-questions/agent`.
  - Pass `sessionId` as a ConvAI dynamic variable when starting the call.

## Frontend

- Routes in `Frontend/src/app/app.routes.ts`. Auth callback: `/callback/oauth`. Live interview: `/interviews/:sessionId/play`.
- CORS origins: `http://localhost:4200` and `https://hackathon-solution.com`.
- After UI changes, verify in the browser (behavior, not only a screenshot). Check every route that shares the state you touched.

## Infrastructure

- Provider `hashicorp/aws` **>= 6.64**. Harness model block is `litellm_model_config`. Output harness **`arn`**, not `harness_arn`.
- DynamoDB tables use `hash_key` / `range_key` (this provider rejects table-level `key_schema`).
- Env: `Infrastructure/env/hackathon.yml`. Backend: `env/hackathon.tfbackend`.
- Path filter for infra CI: `Infrastructure/` **or** `Agents/AgentCore/`.
- Do not commit `.terraform/`, secrets, or `Frontend/src/environments/env.values.ts` if generated.

## Style

- Match the folder you are in. Do not add READMEs next to every feature.
- Do not invent AWS resources the user did not ask for. Prefer YAML + `fileset` over hardcoding harnesses in Terraform.
- No `Co-authored-by` / Cursor trailers on commits.
