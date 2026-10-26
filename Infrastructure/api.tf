data "aws_region" "current" {}

data "aws_caller_identity" "current" {}

locals {
  allowed_origins = [
    "http://localhost:4200",
    "https://${local.domains.frontend}",
  ]

  api_parameter_prefix = "/${local.name_prefix}/${local.env.environment}"

  api_parameters = {
    "JWT/ClientId"        = aws_cognito_user_pool_client.web.id
    "JWT/AuthorityUrl"    = "https://cognito-idp.${data.aws_region.current.region}.amazonaws.com/${aws_cognito_user_pool.main.id}"
    "Cors/AllowedOrigins" = join(",", local.allowed_origins)
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
    ASPNETCORE_ENVIRONMENT = local.env.environment
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

