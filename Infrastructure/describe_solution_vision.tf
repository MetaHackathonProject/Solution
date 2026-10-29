module "describe_solution_vision" {
  source            = "./modules/lambda"
  env               = local.env.environment
  name_prefix       = "${local.name_prefix}-describe-solution-vision"
  lambdas_bucket_id = aws_s3_bucket.lambdas.id
  runtime           = "dotnet10"
  handler           = "Hackathon.DescribeSolutionVision::Hackathon.DescribeSolutionVision.Function::FunctionHandler"
  memory            = 1024
  timeout           = 180

  environment = {
    ASPNETCORE_ENVIRONMENT               = local.env.environment
    APP_InterviewProgress__BucketName    = aws_s3_bucket.interview_sessions.bucket
    APP_PendingInterviewTasks__TableName = aws_dynamodb_table.pending_interview_tasks.name
    APP_MetaModelApi__ApiUrl             = var.meta_model_api_url
    APP_MetaModelApi__ModelId            = var.meta_model_id
    APP_MetaModelApi__ApiKey             = var.meta_model_api_key
  }

  depends_on = [aws_s3_bucket_versioning.lambdas]
}

data "aws_iam_policy_document" "describe_solution_vision" {
  statement {
    effect = "Allow"
    actions = [
      "s3:GetObject",
    ]
    resources = [
      "${aws_s3_bucket.interview_sessions.arn}/*",
    ]
  }

  statement {
    effect = "Allow"
    actions = [
      "dynamodb:GetItem",
      "dynamodb:UpdateItem",
    ]
    resources = [
      aws_dynamodb_table.pending_interview_tasks.arn,
    ]
  }
}

resource "aws_iam_policy" "describe_solution_vision" {
  name   = "${local.name_prefix}-describe-solution-vision-policy-${local.env.environment}"
  policy = data.aws_iam_policy_document.describe_solution_vision.json
}

resource "aws_iam_role_policy_attachment" "describe_solution_vision" {
  role       = module.describe_solution_vision.role_name
  policy_arn = aws_iam_policy.describe_solution_vision.arn
}
