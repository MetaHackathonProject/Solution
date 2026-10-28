# ElevenLabs walkthrough agent

Separate ConvAI agent for post-submit solution discussion. **No** interview-questions MCP (avoids popping the progress queue during the walkthrough).

## Setup

1. Create a new Conversational AI agent in ElevenLabs (same voice/language presets as the main interviewer if you want continuity).
2. Paste `prompt.md` into the agent system prompt (keep `{{job_overview}}`).
3. Add client tool from `tool_configs/finish_solution_walkthrough.json` and attach it (`tool_ids`).
4. Security: allow **First message overrides** (frontend always overrides after submit).
5. Do **not** attach the interview-questions MCP server.
6. Set Terraform `eleven_labs_walkthrough_agent_id` (or `TF_VAR_eleven_labs_walkthrough_agent_id`) to the new agent id, then apply infra so `APP_ElevenLabs__WalkthroughAgentId` is set on the API Lambda.

## Runtime

1. Main interview agent calls `start_interactive_task` → speaks intro → frontend locally ends the ConvAI session (no `end-conversation` API).
2. Candidate submits → frontend `GET /api/voice-agent/signed-url?purpose=walkthrough` → starts this agent with a localized walkthrough first message.
3. Agent calls `finish_solution_walkthrough` → frontend reconnects `purpose=interview` with a localized continue message.
