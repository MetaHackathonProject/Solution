locals {
  name_prefix = var.name_prefix
  filename    = "${var.name_prefix}-${var.env}.zip"
}

resource "aws_s3_object" "function_zip" {
  bucket = var.lambdas_bucket_id
  key    = local.filename
  source = "${path.module}/blank.zip"

  lifecycle {
    ignore_changes = [
      source,
      etag,
    ]
  }
}

resource "aws_lambda_function" "lambda_function" {
  s3_bucket         = var.image_based ? null : var.lambdas_bucket_id
  s3_key            = var.image_based ? null : local.filename
  package_type      = var.image_based ? "Image" : "Zip"
  image_uri         = var.image_based ? var.image_arn : null
  s3_object_version = var.image_based ? null : aws_s3_object.function_zip.version_id
  function_name     = "${local.name_prefix}-${var.env}"
  role              = aws_iam_role.role.arn
  handler           = var.image_based ? null : var.handler
  runtime           = var.image_based ? null : var.runtime
  architectures     = [var.architecture]
  timeout           = var.timeout
  memory_size       = var.memory
  layers            = length(var.layers) > 0 ? var.layers : null

  environment {
    variables = var.environment
  }

  lifecycle {
    ignore_changes = [
      s3_object_version,
      source_code_hash,
    ]
  }

  tags = {
    environment = var.env
  }
}

data "aws_iam_policy_document" "assume_role" {
  statement {
    effect  = "Allow"
    actions = ["sts:AssumeRole"]

    principals {
      type        = "Service"
      identifiers = ["lambda.amazonaws.com"]
    }
  }
}

resource "aws_iam_role" "role" {
  name               = "${local.name_prefix}-role-${var.env}"
  assume_role_policy = data.aws_iam_policy_document.assume_role.json

  lifecycle {
    create_before_destroy = true
  }
}

data "aws_iam_policy_document" "logging" {
  statement {
    effect = "Allow"
    actions = [
      "logs:CreateLogStream",
      "logs:PutLogEvents",
    ]
    resources = [
      aws_cloudwatch_log_group.logs.arn,
      "${aws_cloudwatch_log_group.logs.arn}:*",
    ]
  }
}

resource "aws_iam_policy" "logging" {
  name   = "${local.name_prefix}-logging-${var.env}"
  policy = data.aws_iam_policy_document.logging.json

  lifecycle {
    create_before_destroy = true
  }
}

resource "aws_iam_role_policy_attachment" "logging" {
  role       = aws_iam_role.role.name
  policy_arn = aws_iam_policy.logging.arn
}
