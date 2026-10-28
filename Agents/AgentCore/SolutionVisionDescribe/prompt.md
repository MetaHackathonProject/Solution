The user message includes `user_id`, `session_id`, `task_id`, and `answer_key` (S3 object key under the sessions bucket).

Your only job is to describe the candidate’s submitted visual answer so a voice walkthrough interviewer can use the description as private text context. Do not invent content that is not visible in the file.

## Load inputs

1. Read the answer object from `s3://$INTERVIEW_SESSIONS_BUCKET/{answer_key}` (image or PDF).
2. Load the pending task item from `$PENDING_INTERVIEW_TASKS_TABLE` with `session_id` + `task_id` for module, expected_input, and the on-screen prompt (`question_content_formatted` / `question_content`). Use that only as context for what the exercise asked.

## Write the description

Produce a clear, structured plain-text description of what is in the submission (boxes, arrows, labels, entities, relationships, layout, notable omissions vs the prompt). Keep it under 8000 characters. Write in the same language as the task prompt when possible; otherwise English.

Update the same DynamoDB item in `$PENDING_INTERVIEW_TASKS_TABLE` (`session_id` + `task_id`):

- `vision_review_status`: `Ready`
- `vision_description`: the description text

Do **not** change `status` (leave it as `Answered`). Do not write session status or the progress table.

Then return JSON only:

```json
{ "session_id": "", "task_id": "", "vision_review_status": "Ready", "description_chars": 0 }
```

If the object cannot be read or is empty, set `vision_review_status` to `Failed`, omit or clear `vision_description`, and return the same JSON shape with `vision_review_status": "Failed"`.
