data "aws_iam_policy_document" "interview_planner_assume" {
  statement {
    effect  = "Allow"
    actions = ["sts:AssumeRole"]

    principals {
      type        = "Service"
      identifiers = ["states.amazonaws.com"]
    }
  }
}

resource "aws_iam_role" "interview_planner" {
  name               = "${local.name_prefix}-interview-planner-sfn"
  assume_role_policy = data.aws_iam_policy_document.interview_planner_assume.json
}

data "aws_iam_policy_document" "interview_planner" {
  statement {
    effect = "Allow"
    actions = [
      "bedrock-agentcore:InvokeHarness",
      "bedrock-agentcore:InvokeAgentRuntime",
    ]
    resources = [for harness in aws_bedrockagentcore_harness.agent : harness.arn]
  }

  statement {
    effect    = "Allow"
    actions   = ["dynamodb:UpdateItem"]
    resources = [aws_dynamodb_table.interview_sessions.arn]
  }
}

resource "aws_iam_role_policy" "interview_planner" {
  name   = "${local.name_prefix}-interview-planner-sfn"
  role   = aws_iam_role.interview_planner.id
  policy = data.aws_iam_policy_document.interview_planner.json
}

locals {
  interview_planner_offer_message = "{% 'user_id=' & $user_id & ' session_id=' & $session_id & ' offer_url=' & $offer_url %}"
  interview_planner_user_message  = "{% 'user_id=' & $user_id & ' session_id=' & $session_id & ' recruiter_language=' & $recruiter_language & ' difficulty=' & $difficulty %}"

  interview_planner_status_key = {
    user_id    = { S = "{% $user_id %}" }
    session_id = { S = "{% $session_id %}" }
  }
}

resource "aws_sfn_state_machine" "interview_planner" {
  name     = "${local.name_prefix}-interview-planner"
  role_arn = aws_iam_role.interview_planner.arn

  definition = jsonencode({
    Comment       = "Offer extract, question generation, then interview plan"
    QueryLanguage = "JSONata"
    StartAt       = "Set variables"
    States = {
      "Set variables" = {
        Type = "Pass"
        Assign = {
          user_id            = "{% $states.input.user_id %}"
          session_id         = "{% $states.input.session_id %}"
          offer_url          = "{% $states.input.offer_url %}"
          recruiter_language = "{% $states.input.recruiter_language %}"
          difficulty         = "{% $states.input.difficulty %}"
        }
        Next = "Set status to STARTED"
      }
      "Set status to STARTED" = {
        Type     = "Task"
        Resource = "arn:aws:states:::dynamodb:updateItem"
        Arguments = {
          TableName        = aws_dynamodb_table.interview_sessions.name
          Key              = local.interview_planner_status_key
          UpdateExpression = "SET #status = :status_value"
          ExpressionAttributeNames = {
            "#status" = "status"
          }
          ExpressionAttributeValues = {
            ":status_value" = { S = "STARTED" }
          }
        }
        Output = "{% $states.input %}"
        Next   = "Set status to READING_OFFER"
      }
      "Set status to READING_OFFER" = {
        Type     = "Task"
        Resource = "arn:aws:states:::dynamodb:updateItem"
        Arguments = {
          TableName        = aws_dynamodb_table.interview_sessions.name
          Key              = local.interview_planner_status_key
          UpdateExpression = "SET #status = :status_value"
          ExpressionAttributeNames = {
            "#status" = "status"
          }
          ExpressionAttributeValues = {
            ":status_value" = { S = "READING_OFFER" }
          }
        }
        Output = "{% $states.input %}"
        Next   = "Extract offer"
      }
      "Extract offer" = {
        Type     = "Task"
        Resource = "arn:aws:states:::bedrockagentcore:invokeHarness"
        Arguments = {
          HarnessArn       = aws_bedrockagentcore_harness.agent["offer_extraction"].arn
          RuntimeSessionId = "{% $uuid() %}"
          TimeoutSeconds   = 600
          Messages = [{
            Role    = "user"
            Content = [{ Text = local.interview_planner_offer_message }]
          }]
        }
        Output = "{% $states.input %}"
        Retry = [{
          ErrorEquals     = ["BedrockAgentCore.ThrottlingException"]
          IntervalSeconds = 2
          MaxAttempts     = 3
          BackoffRate     = 2
        }]
        Catch = [{
          ErrorEquals = ["States.ALL"]
          Next        = "Set status to FAILED"
        }]
        Next = "Set status to GENERATING_QUESTIONS"
      }
      "Set status to GENERATING_QUESTIONS" = {
        Type     = "Task"
        Resource = "arn:aws:states:::dynamodb:updateItem"
        Arguments = {
          TableName        = aws_dynamodb_table.interview_sessions.name
          Key              = local.interview_planner_status_key
          UpdateExpression = "SET #status = :status_value"
          ExpressionAttributeNames = {
            "#status" = "status"
          }
          ExpressionAttributeValues = {
            ":status_value" = { S = "GENERATING_QUESTIONS" }
          }
        }
        Output = "{% $states.input %}"
        Next   = "Generate questions"
      }
      "Generate questions" = {
        Type     = "Task"
        Resource = "arn:aws:states:::bedrockagentcore:invokeHarness"
        Arguments = {
          HarnessArn       = aws_bedrockagentcore_harness.agent["question_generation"].arn
          RuntimeSessionId = "{% $uuid() %}"
          TimeoutSeconds   = 600
          Messages = [{
            Role    = "user"
            Content = [{ Text = local.interview_planner_user_message }]
          }]
        }
        Output = "{% $states.input %}"
        Retry = [{
          ErrorEquals     = ["BedrockAgentCore.ThrottlingException"]
          IntervalSeconds = 2
          MaxAttempts     = 3
          BackoffRate     = 2
        }]
        Catch = [{
          ErrorEquals = ["States.ALL"]
          Next        = "Set status to FAILED"
        }]
        Next = "Set status to GENERATING_PLAN"
      }
      "Set status to GENERATING_PLAN" = {
        Type     = "Task"
        Resource = "arn:aws:states:::dynamodb:updateItem"
        Arguments = {
          TableName        = aws_dynamodb_table.interview_sessions.name
          Key              = local.interview_planner_status_key
          UpdateExpression = "SET #status = :status_value"
          ExpressionAttributeNames = {
            "#status" = "status"
          }
          ExpressionAttributeValues = {
            ":status_value" = { S = "GENERATING_PLAN" }
          }
        }
        Output = "{% $states.input %}"
        Next   = "Generate plan"
      }
      "Generate plan" = {
        Type     = "Task"
        Resource = "arn:aws:states:::bedrockagentcore:invokeHarness"
        Arguments = {
          HarnessArn       = aws_bedrockagentcore_harness.agent["interview_plan_generation"].arn
          RuntimeSessionId = "{% $uuid() %}"
          TimeoutSeconds   = 600
          Messages = [{
            Role    = "user"
            Content = [{ Text = local.interview_planner_user_message }]
          }]
        }
        Output = "{% $states.input %}"
        Retry = [{
          ErrorEquals     = ["BedrockAgentCore.ThrottlingException"]
          IntervalSeconds = 2
          MaxAttempts     = 3
          BackoffRate     = 2
        }]
        Catch = [{
          ErrorEquals = ["States.ALL"]
          Next        = "Set status to FAILED"
        }]
        Next = "Set status to SUCCESS"
      }
      "Set status to SUCCESS" = {
        Type     = "Task"
        Resource = "arn:aws:states:::dynamodb:updateItem"
        Arguments = {
          TableName        = aws_dynamodb_table.interview_sessions.name
          Key              = local.interview_planner_status_key
          UpdateExpression = "SET #status = :status_value"
          ExpressionAttributeNames = {
            "#status" = "status"
          }
          ExpressionAttributeValues = {
            ":status_value" = { S = "SUCCESS" }
          }
        }
        End = true
      }
      "Set status to FAILED" = {
        Type     = "Task"
        Resource = "arn:aws:states:::dynamodb:updateItem"
        Arguments = {
          TableName        = aws_dynamodb_table.interview_sessions.name
          Key              = local.interview_planner_status_key
          UpdateExpression = "SET #status = :status_value"
          ExpressionAttributeNames = {
            "#status" = "status"
          }
          ExpressionAttributeValues = {
            ":status_value" = { S = "FAILED" }
          }
        }
        End = true
      }
    }
  })

  depends_on = [aws_iam_role_policy.interview_planner]
}
