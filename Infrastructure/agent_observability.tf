data "aws_iam_policy_document" "harness_transaction_search" {
  statement {
    sid    = "TransactionSearchXRayAccess"
    effect = "Allow"

    principals {
      type        = "Service"
      identifiers = ["xray.amazonaws.com"]
    }

    actions = ["logs:PutLogEvents"]

    resources = [
      "arn:aws:logs:${data.aws_region.current.region}:${data.aws_caller_identity.current.account_id}:log-group:aws/spans:*",
      "arn:aws:logs:${data.aws_region.current.region}:${data.aws_caller_identity.current.account_id}:log-group:/aws/application-signals/data:*",
    ]

    condition {
      test     = "ArnLike"
      variable = "aws:SourceArn"
      values   = ["arn:aws:xray:${data.aws_region.current.region}:${data.aws_caller_identity.current.account_id}:*"]
    }

    condition {
      test     = "StringEquals"
      variable = "aws:SourceAccount"
      values   = [data.aws_caller_identity.current.account_id]
    }
  }
}

resource "aws_cloudwatch_log_resource_policy" "harness_transaction_search" {
  policy_name     = "${local.name_prefix}-xray-transaction-search"
  policy_document = data.aws_iam_policy_document.harness_transaction_search.json
}

resource "aws_xray_trace_segment_destination" "cloudwatch_logs" {
  destination = "CloudWatchLogs"
}

resource "aws_cloudwatch_log_group" "harness_invocation" {
  for_each = local.agents

  name              = "/aws/vendedlogs/bedrock-agentcore/harness/${each.key}"
  retention_in_days = 5
}

resource "aws_cloudwatch_log_delivery_source" "harness_logs" {
  for_each = local.agents

  name         = "${local.name_prefix}-${replace(each.key, "_", "-")}-logs"
  log_type     = "APPLICATION_LOGS"
  resource_arn = aws_bedrockagentcore_harness.agent[each.key].arn
}

resource "aws_cloudwatch_log_delivery_source" "harness_traces" {
  for_each = local.agents

  name         = "${local.name_prefix}-${replace(each.key, "_", "-")}-traces"
  log_type     = "TRACES"
  resource_arn = aws_bedrockagentcore_harness.agent[each.key].arn
}

resource "aws_cloudwatch_log_delivery_destination" "harness_logs" {
  for_each = local.agents

  name          = "${local.name_prefix}-${replace(each.key, "_", "-")}-logs"
  output_format = "json"

  delivery_destination_configuration {
    destination_resource_arn = aws_cloudwatch_log_group.harness_invocation[each.key].arn
  }
}

resource "aws_cloudwatch_log_delivery_destination" "harness_traces" {
  name                      = "${local.name_prefix}-harness-traces"
  delivery_destination_type = "XRAY"
}

resource "aws_cloudwatch_log_delivery" "harness_logs" {
  for_each = local.agents

  delivery_source_name     = aws_cloudwatch_log_delivery_source.harness_logs[each.key].name
  delivery_destination_arn = aws_cloudwatch_log_delivery_destination.harness_logs[each.key].arn
}

resource "aws_cloudwatch_log_delivery" "harness_traces" {
  for_each = local.agents

  delivery_source_name     = aws_cloudwatch_log_delivery_source.harness_traces[each.key].name
  delivery_destination_arn = aws_cloudwatch_log_delivery_destination.harness_traces.arn
}
