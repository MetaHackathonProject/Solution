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
