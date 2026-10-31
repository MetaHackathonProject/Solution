resource "aws_dynamodb_table" "interview_sessions" {
  name         = "${local.name_prefix}-interview-sessions-table"
  billing_mode = "PAY_PER_REQUEST"
  hash_key     = "user_id"
  range_key    = "session_id"

  attribute {
    name = "user_id"
    type = "S"
  }

  attribute {
    name = "session_id"
    type = "S"
  }
}

resource "aws_dynamodb_table" "interview_session_questions" {
  name         = "${local.name_prefix}-interview-session-questions-table"
  billing_mode = "PAY_PER_REQUEST"
  hash_key     = "session_id"
  range_key    = "question_id"

  attribute {
    name = "session_id"
    type = "S"
  }

  attribute {
    name = "question_id"
    type = "S"
  }

  attribute {
    name = "module"
    type = "S"
  }

  global_secondary_index {
    name            = "module-index"
    hash_key        = "session_id"
    range_key       = "module"
    projection_type = "ALL"
  }
}

resource "aws_dynamodb_table" "interview_sessions_progress" {
  name         = "${local.name_prefix}-interview-sessions-progress-table"
  billing_mode = "PAY_PER_REQUEST"
  hash_key     = "session_id"
  range_key    = "question_id"

  attribute {
    name = "session_id"
    type = "S"
  }

  attribute {
    name = "question_id"
    type = "S"
  }

  attribute {
    name = "order_key"
    type = "S"
  }

  global_secondary_index {
    name            = "order-key-index"
    hash_key        = "session_id"
    range_key       = "order_key"
    projection_type = "ALL"
  }
}

resource "aws_dynamodb_table" "pending_interview_tasks" {
  name         = "${local.name_prefix}-pending-interview-tasks-table"
  billing_mode = "PAY_PER_REQUEST"
  hash_key     = "session_id"
  range_key    = "task_id"

  attribute {
    name = "session_id"
    type = "S"
  }

  attribute {
    name = "task_id"
    type = "S"
  }
}

resource "aws_dynamodb_table" "user_profiles" {
  name         = "${local.name_prefix}-user-profiles-table"
  billing_mode = "PAY_PER_REQUEST"
  hash_key     = "user_id"

  attribute {
    name = "user_id"
    type = "S"
  }
}

resource "aws_dynamodb_table" "interview_templates" {
  name         = "${local.name_prefix}-interview-templates-table"
  billing_mode = "PAY_PER_REQUEST"
  hash_key     = "employer_user_id"
  range_key    = "template_id"

  attribute {
    name = "employer_user_id"
    type = "S"
  }

  attribute {
    name = "template_id"
    type = "S"
  }

  attribute {
    name = "invite_code"
    type = "S"
  }

  global_secondary_index {
    name            = "invite_code_index"
    hash_key        = "invite_code"
    projection_type = "ALL"
  }
}

resource "aws_dynamodb_table" "interview_assignments" {
  name         = "${local.name_prefix}-interview-assignments-table"
  billing_mode = "PAY_PER_REQUEST"
  hash_key     = "session_id"

  attribute {
    name = "session_id"
    type = "S"
  }

  attribute {
    name = "employer_user_id"
    type = "S"
  }

  attribute {
    name = "candidate_user_id"
    type = "S"
  }

  attribute {
    name = "template_id"
    type = "S"
  }

  global_secondary_index {
    name            = "employer_user_id_index"
    hash_key        = "employer_user_id"
    projection_type = "ALL"
  }

  global_secondary_index {
    name            = "candidate_template_index"
    hash_key        = "candidate_user_id"
    range_key       = "template_id"
    projection_type = "ALL"
  }
}

# ElevenLabs ConvAI conversation ids per session (interview + walkthrough legs).
resource "aws_dynamodb_table" "interview_session_conversations" {
  name         = "${local.name_prefix}-interview-sessions-conversations-table"
  billing_mode = "PAY_PER_REQUEST"
  hash_key     = "session_id"
  range_key    = "created_at"

  attribute {
    name = "session_id"
    type = "S"
  }

  attribute {
    name = "created_at"
    type = "N"
  }
}
