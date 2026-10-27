module "seed_interview_progress" {
  source            = "./modules/lambda"
  env               = local.env.environment
  name_prefix       = "${local.name_prefix}-seed-interview-progress"
  lambdas_bucket_id = aws_s3_bucket.lambdas.id
  runtime           = "dotnet10"
  handler           = "Hackathon.SeedInterviewProgress::Hackathon.SeedInterviewProgress.Function::FunctionHandler"
  memory            = 256
  timeout           = 30

  environment = {
    ASPNETCORE_ENVIRONMENT                 = local.env.environment
    APP_InterviewProgress__TableName       = aws_dynamodb_table.interview_sessions_progress.name
    APP_InterviewProgress__BucketName      = aws_s3_bucket.interview_sessions.bucket
    APP_PendingInterviewTasks__TableName   = aws_dynamodb_table.pending_interview_tasks.name
  }

  depends_on = [aws_s3_bucket_versioning.lambdas]
}

data "aws_iam_policy_document" "seed_interview_progress" {
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
      "dynamodb:Query",
      "dynamodb:GetItem",
      "dynamodb:PutItem",
      "dynamodb:DeleteItem",
      "dynamodb:BatchWriteItem",
    ]
    resources = [
      aws_dynamodb_table.interview_sessions_progress.arn,
      "${aws_dynamodb_table.interview_sessions_progress.arn}/index/*",
      aws_dynamodb_table.pending_interview_tasks.arn,
    ]
  }
}

resource "aws_iam_policy" "seed_interview_progress" {
  name   = "${local.name_prefix}-seed-interview-progress-policy-${local.env.environment}"
  policy = data.aws_iam_policy_document.seed_interview_progress.json
}

resource "aws_iam_role_policy_attachment" "seed_interview_progress" {
  role       = module.seed_interview_progress.role_name
  policy_arn = aws_iam_policy.seed_interview_progress.arn
}
