data "aws_region" "current" {}

data "aws_caller_identity" "current" {}

locals {
  allowed_origins = [
    "http://localhost:4200",
    "https://${local.domains.frontend}",
  ]

  api_parameter_prefix = "/${local.name_prefix}/${local.env.environment}"

  api_parameters = {
    "JWT/ClientId"                           = aws_cognito_user_pool_client.web.id
    "JWT/AuthorityUrl"                       = "https://cognito-idp.${data.aws_region.current.region}.amazonaws.com/${aws_cognito_user_pool.main.id}"
    "Cors/AllowedOrigins"                    = join(",", local.allowed_origins)
    "InterviewSessions/TableName"            = aws_dynamodb_table.interview_sessions.name
    "InterviewSessions/QuestionsTableName"   = aws_dynamodb_table.interview_session_questions.name
    "InterviewSessions/ProgressTableName"    = aws_dynamodb_table.interview_sessions_progress.name
    "InterviewSessions/BucketName"           = aws_s3_bucket.interview_sessions.bucket
    "InterviewProgress/TableName"            = aws_dynamodb_table.interview_sessions_progress.name
    "InterviewProgress/BucketName"           = aws_s3_bucket.interview_sessions.bucket
    "PendingInterviewTasks/TableName"        = aws_dynamodb_table.pending_interview_tasks.name
    "InterviewConversations/TableName"       = aws_dynamodb_table.interview_session_conversations.name
    "InterviewPlanner/StateMachineArn"       = aws_sfn_state_machine.interview_planner.arn
    "SolutionVisionReview/StateMachineArn"   = aws_sfn_state_machine.solution_vision_review.arn
    "InterviewOverview/StateMachineArn"      = aws_sfn_state_machine.interview_overview.arn
    "ElevenLabs/ApiUrl"                      = var.eleven_labs_api_url
    "ElevenLabs/AgentId"                     = var.eleven_labs_agent_id
    "ElevenLabs/WalkthroughAgentId"          = var.eleven_labs_walkthrough_agent_id
  }

  api_secure_parameters = {
    "ElevenLabs/ApiKey" = var.eleven_labs_api_key
  }
}

module "api" {
  source            = "./modules/lambda"
  env               = local.env.environment
  name_prefix       = "${local.name_prefix}-api"
  lambdas_bucket_id = aws_s3_bucket.lambdas.id
  runtime           = "dotnet10"
  handler           = "Hackathon.Api::Hackathon.Api.LambdaEntryPoint::FunctionHandlerAsync"
  memory            = 512

  environment = {
    ASPNETCORE_ENVIRONMENT                     = local.env.environment
    APP_InterviewSessions__TableName           = aws_dynamodb_table.interview_sessions.name
    APP_InterviewSessions__QuestionsTableName  = aws_dynamodb_table.interview_session_questions.name
    APP_InterviewSessions__ProgressTableName   = aws_dynamodb_table.interview_sessions_progress.name
    APP_InterviewSessions__BucketName          = aws_s3_bucket.interview_sessions.bucket
    APP_InterviewProgress__TableName           = aws_dynamodb_table.interview_sessions_progress.name
    APP_InterviewProgress__BucketName          = aws_s3_bucket.interview_sessions.bucket
    APP_PendingInterviewTasks__TableName       = aws_dynamodb_table.pending_interview_tasks.name
    APP_InterviewConversations__TableName      = aws_dynamodb_table.interview_session_conversations.name
    APP_InterviewPlanner__StateMachineArn      = aws_sfn_state_machine.interview_planner.arn
    APP_SolutionVisionReview__StateMachineArn  = aws_sfn_state_machine.solution_vision_review.arn
    APP_InterviewOverview__StateMachineArn     = aws_sfn_state_machine.interview_overview.arn
    APP_ElevenLabs__ApiUrl                     = var.eleven_labs_api_url
    APP_ElevenLabs__AgentId                    = var.eleven_labs_agent_id
    APP_ElevenLabs__WalkthroughAgentId         = var.eleven_labs_walkthrough_agent_id
    APP_ElevenLabs__ApiKey                     = var.eleven_labs_api_key
  }

  depends_on = [aws_s3_bucket_versioning.lambdas]
}

resource "aws_lambda_permission" "api" {
  statement_id  = "AllowAPIGatewayInvoke"
  action        = "lambda:InvokeFunction"
  function_name = module.api.function_name
  principal     = "apigateway.amazonaws.com"
  source_arn    = "${aws_apigatewayv2_api.http_api.execution_arn}/*/*"
}

data "aws_iam_policy_document" "api" {
  statement {
    effect = "Allow"
    actions = [
      "ssm:GetParameter",
      "ssm:GetParameters",
      "ssm:GetParametersByPath",
    ]
    resources = [
      "arn:aws:ssm:${data.aws_region.current.region}:${data.aws_caller_identity.current.account_id}:parameter${local.api_parameter_prefix}",
      "arn:aws:ssm:${data.aws_region.current.region}:${data.aws_caller_identity.current.account_id}:parameter${local.api_parameter_prefix}/*",
    ]
  }

  statement {
    effect = "Allow"
    actions = [
      "dynamodb:PutItem",
      "dynamodb:GetItem",
      "dynamodb:UpdateItem",
      "dynamodb:DeleteItem",
      "dynamodb:Query",
      "dynamodb:BatchWriteItem",
    ]
    resources = [
      aws_dynamodb_table.interview_sessions.arn,
      aws_dynamodb_table.interview_session_questions.arn,
      "${aws_dynamodb_table.interview_session_questions.arn}/index/*",
      aws_dynamodb_table.interview_sessions_progress.arn,
      "${aws_dynamodb_table.interview_sessions_progress.arn}/index/*",
      aws_dynamodb_table.pending_interview_tasks.arn,
      aws_dynamodb_table.interview_session_conversations.arn,
    ]
  }

  statement {
    effect = "Allow"
    actions = [
      "s3:ListBucket",
    ]
    resources = [aws_s3_bucket.interview_sessions.arn]
  }

  statement {
    effect = "Allow"
    actions = [
      "s3:GetObject",
      "s3:PutObject",
      "s3:DeleteObject",
    ]
    resources = ["${aws_s3_bucket.interview_sessions.arn}/*"]
  }

  statement {
    effect  = "Allow"
    actions = ["states:StartExecution"]
    resources = [
      aws_sfn_state_machine.interview_planner.arn,
      aws_sfn_state_machine.solution_vision_review.arn,
      aws_sfn_state_machine.interview_overview.arn,
    ]
  }
}

resource "aws_iam_policy" "api" {
  name   = "${local.name_prefix}-api-policy-${local.env.environment}"
  policy = data.aws_iam_policy_document.api.json
}

resource "aws_iam_role_policy_attachment" "api" {
  role       = module.api.role_name
  policy_arn = aws_iam_policy.api.arn
}

resource "aws_ssm_parameter" "api" {
  for_each = local.api_parameters

  name  = "${local.api_parameter_prefix}/${each.key}"
  type  = "String"
  value = each.value
}

resource "aws_ssm_parameter" "api_secure" {
  for_each = local.api_secure_parameters

  name  = "${local.api_parameter_prefix}/${each.key}"
  type  = "SecureString"
  value = each.value

  lifecycle {
    ignore_changes = [value]
  }
}

