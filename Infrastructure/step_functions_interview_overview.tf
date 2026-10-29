data "aws_iam_policy_document" "interview_overview_assume" {
  statement {
    effect  = "Allow"
    actions = ["sts:AssumeRole"]

    principals {
      type        = "Service"
      identifiers = ["states.amazonaws.com"]
    }
  }
}

resource "aws_iam_role" "interview_overview" {
  name               = "${local.name_prefix}-interview-overview-sfn"
  assume_role_policy = data.aws_iam_policy_document.interview_overview_assume.json
}

data "aws_iam_policy_document" "interview_overview" {
  statement {
    effect    = "Allow"
    actions   = ["lambda:InvokeFunction"]
    resources = [module.build_interview_overview.function_arn]
  }

  statement {
    effect    = "Allow"
    actions   = ["dynamodb:UpdateItem"]
    resources = [aws_dynamodb_table.interview_sessions.arn]
  }
}

resource "aws_iam_role_policy" "interview_overview" {
  name   = "${local.name_prefix}-interview-overview-sfn"
  role   = aws_iam_role.interview_overview.id
  policy = data.aws_iam_policy_document.interview_overview.json
}

locals {
  interview_overview_session_key = {
    user_id    = { S = "{% $user_id %}" }
    session_id = { S = "{% $session_id %}" }
  }
}

resource "aws_sfn_state_machine" "interview_overview" {
  name     = "${local.name_prefix}-interview-overview"
  role_arn = aws_iam_role.interview_overview.arn

  definition = jsonencode({
    Comment       = "Fetch transcripts, judge interactive answers, compose overview"
    QueryLanguage = "JSONata"
    StartAt       = "Set variables"
    States = {
      "Set variables" = {
        Type = "Pass"
        Assign = {
          user_id    = "{% $states.input.user_id %}"
          session_id = "{% $states.input.session_id %}"
        }
        Next = "Build overview"
      }
      "Build overview" = {
        Type     = "Task"
        Resource = "arn:aws:states:::lambda:invoke"
        Arguments = {
          FunctionName = module.build_interview_overview.function_arn
          Payload = {
            user_id    = "{% $user_id %}"
            session_id = "{% $session_id %}"
          }
        }
        Output = "{% $states.input %}"
        Retry = [{
          ErrorEquals     = ["Lambda.ServiceException", "Lambda.AWSLambdaException", "Lambda.SdkClientException", "Lambda.TooManyRequestsException"]
          IntervalSeconds = 2
          MaxAttempts     = 3
          BackoffRate     = 2
        }]
        Catch = [{
          ErrorEquals = ["States.ALL"]
          Next        = "Set overview ERROR"
        }]
        End = true
      }
      "Set overview ERROR" = {
        Type     = "Task"
        Resource = "arn:aws:states:::dynamodb:updateItem"
        Arguments = {
          TableName        = aws_dynamodb_table.interview_sessions.name
          Key              = local.interview_overview_session_key
          UpdateExpression = "SET overview_status = :status_value"
          ExpressionAttributeValues = {
            ":status_value" = { S = "ERROR" }
          }
        }
        End = true
      }
    }
  })

  depends_on = [aws_iam_role_policy.interview_overview]
}
