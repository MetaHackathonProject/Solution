data "aws_iam_policy_document" "solution_vision_review_assume" {
  statement {
    effect  = "Allow"
    actions = ["sts:AssumeRole"]

    principals {
      type        = "Service"
      identifiers = ["states.amazonaws.com"]
    }
  }
}

resource "aws_iam_role" "solution_vision_review" {
  name               = "${local.name_prefix}-solution-vision-review-sfn"
  assume_role_policy = data.aws_iam_policy_document.solution_vision_review_assume.json
}

data "aws_iam_policy_document" "solution_vision_review" {
  statement {
    effect = "Allow"
    actions = [
      "bedrock-agentcore:InvokeHarness",
      "bedrock-agentcore:InvokeAgentRuntime",
    ]
    resources = [aws_bedrockagentcore_harness.agent["solution_vision_describe"].arn]
  }

  statement {
    effect    = "Allow"
    actions   = ["dynamodb:UpdateItem"]
    resources = [aws_dynamodb_table.pending_interview_tasks.arn]
  }
}

resource "aws_iam_role_policy" "solution_vision_review" {
  name   = "${local.name_prefix}-solution-vision-review-sfn"
  role   = aws_iam_role.solution_vision_review.id
  policy = data.aws_iam_policy_document.solution_vision_review.json
}

locals {
  solution_vision_review_message = "{% 'user_id=' & $user_id & ' session_id=' & $session_id & ' task_id=' & $task_id & ' answer_key=' & $answer_key %}"

  solution_vision_review_task_key = {
    session_id = { S = "{% $session_id %}" }
    task_id    = { S = "{% $task_id %}" }
  }
}

resource "aws_sfn_state_machine" "solution_vision_review" {
  name     = "${local.name_prefix}-solution-vision-review"
  role_arn = aws_iam_role.solution_vision_review.arn

  definition = jsonencode({
    Comment       = "Describe interactive visual answers for the walkthrough agent"
    QueryLanguage = "JSONata"
    StartAt       = "Set variables"
    States = {
      "Set variables" = {
        Type = "Pass"
        Assign = {
          user_id    = "{% $states.input.user_id %}"
          session_id = "{% $states.input.session_id %}"
          task_id    = "{% $states.input.task_id %}"
          answer_key = "{% $states.input.answer_key %}"
        }
        Next = "Set vision review Running"
      }
      "Set vision review Running" = {
        Type     = "Task"
        Resource = "arn:aws:states:::dynamodb:updateItem"
        Arguments = {
          TableName        = aws_dynamodb_table.pending_interview_tasks.name
          Key              = local.solution_vision_review_task_key
          UpdateExpression = "SET vision_review_status = :status_value"
          ExpressionAttributeValues = {
            ":status_value" = { S = "Running" }
          }
        }
        Output = "{% $states.input %}"
        Next   = "Describe solution"
      }
      "Describe solution" = {
        Type     = "Task"
        Resource = "arn:aws:states:::bedrockagentcore:invokeHarness"
        Arguments = {
          HarnessArn       = aws_bedrockagentcore_harness.agent["solution_vision_describe"].arn
          RuntimeSessionId = "{% $uuid() %}"
          TimeoutSeconds   = 180
          Messages = [{
            Role    = "user"
            Content = [{ Text = local.solution_vision_review_message }]
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
          Next        = "Set vision review Failed"
        }]
        # Agent writes Ready + vision_description (or Failed). SF only forces Failed on invoke errors.
        End = true
      }
      "Set vision review Failed" = {
        Type     = "Task"
        Resource = "arn:aws:states:::dynamodb:updateItem"
        Arguments = {
          TableName        = aws_dynamodb_table.pending_interview_tasks.name
          Key              = local.solution_vision_review_task_key
          UpdateExpression = "SET vision_review_status = :status_value"
          ExpressionAttributeValues = {
            ":status_value" = { S = "Failed" }
          }
        }
        End = true
      }
    }
  })

  depends_on = [aws_iam_role_policy.solution_vision_review]
}
