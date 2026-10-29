module "build_interview_overview" {
  source            = "./modules/lambda"
  env               = local.env.environment
  name_prefix       = "${local.name_prefix}-build-interview-overview"
  lambdas_bucket_id = aws_s3_bucket.lambdas.id
  runtime           = "dotnet10"
  handler           = "Hackathon.BuildInterviewOverview::Hackathon.BuildInterviewOverview.Function::FunctionHandler"
  memory            = 2048
  timeout           = 300

  environment = {
    ASPNETCORE_ENVIRONMENT                 = local.env.environment
    APP_InterviewSessions__TableName       = aws_dynamodb_table.interview_sessions.name
    APP_InterviewProgress__BucketName      = aws_s3_bucket.interview_sessions.bucket
    APP_PendingInterviewTasks__TableName   = aws_dynamodb_table.pending_interview_tasks.name
    APP_InterviewConversations__TableName  = aws_dynamodb_table.interview_session_conversations.name
    APP_ElevenLabs__ApiUrl                 = var.eleven_labs_api_url
    APP_ElevenLabs__ApiKey                 = var.eleven_labs_api_key
    APP_ElevenLabs__AgentId                = var.eleven_labs_agent_id
    APP_ElevenLabs__WalkthroughAgentId     = var.eleven_labs_walkthrough_agent_id
    APP_MetaModelApi__ApiUrl               = var.meta_model_api_url
    APP_MetaModelApi__ModelId              = var.meta_model_id
    APP_MetaModelApi__WonderModelId        = var.meta_wonder_model_id
    APP_MetaModelApi__ApiKey               = var.meta_model_api_key
  }

  depends_on = [aws_s3_bucket_versioning.lambdas]
}

data "aws_iam_policy_document" "build_interview_overview" {
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
    ]
    resources = ["${aws_s3_bucket.interview_sessions.arn}/*"]
  }

  statement {
    effect = "Allow"
    actions = [
      "dynamodb:GetItem",
      "dynamodb:UpdateItem",
      "dynamodb:Query",
    ]
    resources = [
      aws_dynamodb_table.interview_sessions.arn,
      aws_dynamodb_table.pending_interview_tasks.arn,
      aws_dynamodb_table.interview_session_conversations.arn,
    ]
  }
}

resource "aws_iam_policy" "build_interview_overview" {
  name   = "${local.name_prefix}-build-interview-overview-policy-${local.env.environment}"
  policy = data.aws_iam_policy_document.build_interview_overview.json
}

resource "aws_iam_role_policy_attachment" "build_interview_overview" {
  role       = module.build_interview_overview.role_name
  policy_arn = aws_iam_policy.build_interview_overview.arn
}
