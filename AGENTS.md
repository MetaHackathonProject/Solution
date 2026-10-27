# Agent instructions

This repo is a mock-interview product: extract a job offer, generate questions, curate a spoken plan, then run the interview. AWS is **eu-central-1**. Do not invent credentials, API keys, or offer facts that are not in the posting.

## Layout

- `Frontend/` — Angular 20, Cognito OIDC (`angular-auth-oidc-client`). Env via `Frontend/scripts/load-env.mjs` (`COGNITO_AUTHORITY`, `COGNITO_CLIENT_ID`).
- `Backend/` — .NET 10, MediatR. `Hackathon.Api` → `Hackathon.Application` → `Hackathon.Domain`. `Hackathon.Infrastructure` is AWS adapters. Lambda is an **Event Function** (`Amazon.Lambda.AspNetCoreServer`), not a Web Function.
- `Infrastructure/` — Terraform. Agents are not defined here; Terraform reads YAML.
- `Agents/AgentCore/<Name>/` — one harness per folder. This is the only AgentCore source of truth.
- `Agents/ElevenLabsAgent/` — voice agent export. Do not Terraform it.

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
2. `question_generation` — read details, write DynamoDB spoken question items (`session_id`, `question_id` UUID, `module`, `question`, `assessment_goal`) **and** exactly 2 interactive tasks to `$PENDING_INTERVIEW_TASKS_TABLE` (`session_id`, `task_id`, `module`, `question_content`, `question_content_formatted`, `expected_input`, `status=NotAsked`, `assessment_goal`)
3. `interview_plan_generation` — read details + questions + pending tasks, write `.../{user_id}/{session_id}/plan.json` only (always include both interactive tasks with `type: "interactive"` + `task_id`, on top of the 10–12 spoken cap)

Do **not** have agents write the progress table. That table is for live-interview runtime.

Orchestrate with Step Functions (`Infrastructure/step_functions.tf`), not a supervisor agent. After plan generation, Step Functions invokes `Hackathon.SeedInterviewProgress` to fill the progress table from `plan.json`, then sets `SUCCESS`. Statuses live on the sessions table attribute `status`: `STARTED`, `READING_OFFER`, `GENERATING_QUESTIONS`, `GENERATING_PLAN`, `SUCCESS`, `IN_PROGRESS`, `COMPLETED`, `FAILED`. The API persists the session then `states:StartExecution`. Do not have agents write status.

Live interview (API + frontend, not AgentCore):

1. `GET /api/voice-agent/signed-url?sessionId=` — ElevenLabs ConvAI signed URL; sets status `IN_PROGRESS` when session is `SUCCESS` or `IN_PROGRESS`.
2. `GET /api/interview-sessions/{sessionId}/voice-context` — job title/company/overview/language for ConvAI dynamic variables.
3. Frontend `/interviews/:sessionId/play` starts `@elevenlabs/client` with `sessionId`, `job_title`, `company`, `job_overview`.
4. `POST /api/voice-agent/end-conversation` — if the progress table has no remaining rows **and** no pending interactive tasks are `NotAsked`/`InProgress`, sets `COMPLETED`; otherwise leaves status alone (no transcription/orders).
5. Interactive tasks: `GET /api/interview-sessions/{sessionId}/interactive-task` (active / resume), `GET .../interactive-task/{taskId}`, `POST .../answer` (multipart `text` or `file` → S3 `{user_id}/{session_id}/interactive-tasks/{task_id}/answer.*`). Voice agent client tool `start_interactive_task(taskId)` opens the play UI panel.

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
- Live questions MCP: `Hackathon.Mcp` exposes `get_next_question(sessionId)` at `https://api.hackathon-solution.com/mcp/interview-questions/agent` (also `/mcp/interview-questions`; public, no JWT). Pops up to 2 ordered progress rows. If the next item is interactive, pops only that one and sets pending status `InProgress`; returns `type`/`taskId` on interactive items.
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
