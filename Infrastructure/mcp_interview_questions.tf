module "mcp_interview_questions" {
  source            = "./modules/lambda"
  env               = local.env.environment
  name_prefix       = "${local.name_prefix}-mcp-interview-questions"
  lambdas_bucket_id = aws_s3_bucket.lambdas.id
  runtime           = "dotnet10"
  handler           = "Hackathon.Mcp"
  memory            = 512
  timeout           = 30

  environment = {
    ASPNETCORE_ENVIRONMENT                 = local.env.environment
    APP_InterviewProgress__TableName       = aws_dynamodb_table.interview_sessions_progress.name
    APP_InterviewProgress__BucketName      = aws_s3_bucket.interview_sessions.bucket
    APP_PendingInterviewTasks__TableName   = aws_dynamodb_table.pending_interview_tasks.name
    APP_InterviewSessions__QuestionsTableName = aws_dynamodb_table.interview_session_questions.name
  }

  depends_on = [aws_s3_bucket_versioning.lambdas]
}

resource "aws_lambda_permission" "mcp_interview_questions" {
  statement_id  = "AllowAPIGatewayInvoke"
  action        = "lambda:InvokeFunction"
  function_name = module.mcp_interview_questions.function_name
  principal     = "apigateway.amazonaws.com"
  source_arn    = "${aws_apigatewayv2_api.http_api.execution_arn}/*/*"
}

data "aws_iam_policy_document" "mcp_interview_questions" {
  statement {
    effect = "Allow"
    actions = [
      "dynamodb:Query",
      "dynamodb:GetItem",
      "dynamodb:PutItem",
      "dynamodb:UpdateItem",
      "dynamodb:DeleteItem",
      "dynamodb:BatchWriteItem",
    ]
    resources = [
      aws_dynamodb_table.interview_sessions_progress.arn,
      "${aws_dynamodb_table.interview_sessions_progress.arn}/index/*",
      aws_dynamodb_table.pending_interview_tasks.arn,
      aws_dynamodb_table.interview_session_questions.arn,
      "${aws_dynamodb_table.interview_session_questions.arn}/index/*",
    ]
  }
}

resource "aws_iam_policy" "mcp_interview_questions" {
  name   = "${local.name_prefix}-mcp-interview-questions-policy-${local.env.environment}"
  policy = data.aws_iam_policy_document.mcp_interview_questions.json
}

resource "aws_iam_role_policy_attachment" "mcp_interview_questions" {
  role       = module.mcp_interview_questions.role_name
  policy_arn = aws_iam_policy.mcp_interview_questions.arn
}

resource "aws_apigatewayv2_integration" "mcp_interview_questions" {
  api_id                 = aws_apigatewayv2_api.http_api.id
  integration_type       = "AWS_PROXY"
  integration_uri        = module.mcp_interview_questions.function_arn
  integration_method     = "POST"
  payload_format_version = "2.0"
}

resource "aws_apigatewayv2_route" "mcp_interview_questions_root" {
  for_each = local.api_methods

  api_id             = aws_apigatewayv2_api.http_api.id
  route_key          = "${each.value} /mcp/interview-questions"
  target             = "integrations/${aws_apigatewayv2_integration.mcp_interview_questions.id}"
  authorization_type = "NONE"
}

resource "aws_apigatewayv2_route" "mcp_interview_questions" {
  for_each = local.api_methods

  api_id             = aws_apigatewayv2_api.http_api.id
  route_key          = "${each.value} /mcp/interview-questions/{proxy+}"
  target             = "integrations/${aws_apigatewayv2_integration.mcp_interview_questions.id}"
  authorization_type = "NONE"
}
