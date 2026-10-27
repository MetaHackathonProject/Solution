locals {
  agents_root = "${path.module}/../Agents/AgentCore"
  agent_files = fileset(local.agents_root, "*/agent.yaml")

  agents = {
    for agent_file in local.agent_files :
    yamldecode(file("${local.agents_root}/${agent_file}")).name => {
      config = yamldecode(file("${local.agents_root}/${agent_file}"))
      prompt = file("${local.agents_root}/${dirname(agent_file)}/prompt.md")
    }
  }
}

data "aws_iam_policy_document" "agent_assume" {
  statement {
    effect  = "Allow"
    actions = ["sts:AssumeRole"]

    principals {
      type        = "Service"
      identifiers = ["bedrock-agentcore.amazonaws.com"]
    }
  }
}

resource "aws_iam_role" "agent" {
  for_each = local.agents

  name               = "${local.name_prefix}-${replace(each.key, "_", "-")}-harness"
  assume_role_policy = data.aws_iam_policy_document.agent_assume.json
}

data "aws_iam_policy_document" "agent" {
  for_each = local.agents

  statement {
    effect = "Allow"
    actions = [
      "ecr-public:GetAuthorizationToken",
      "sts:GetServiceBearerToken",
    ]
    resources = ["*"]
  }

  statement {
    effect = "Allow"
    actions = [
      "logs:CreateLogGroup",
      "logs:DescribeLogStreams",
      "logs:CreateLogStream",
      "logs:PutLogEvents",
    ]
    resources = [
      "arn:aws:logs:${data.aws_region.current.region}:${data.aws_caller_identity.current.account_id}:log-group:/aws/bedrock-agentcore/runtimes/*",
      "arn:aws:logs:${data.aws_region.current.region}:${data.aws_caller_identity.current.account_id}:log-group:/aws/bedrock-agentcore/runtimes/*:log-stream:*",
    ]
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
      "dynamodb:PutItem",
      "dynamodb:BatchWriteItem",
      "dynamodb:Query",
    ]
    resources = [
      aws_dynamodb_table.interview_session_questions.arn,
      "${aws_dynamodb_table.interview_session_questions.arn}/index/*",
    ]
  }

  statement {
    effect = "Allow"
    actions = [
      "bedrock-agentcore:GetWorkloadAccessToken",
      "bedrock-agentcore:GetWorkloadAccessTokenForJWT",
    ]
    resources = [
      "arn:aws:bedrock-agentcore:${data.aws_region.current.region}:${data.aws_caller_identity.current.account_id}:workload-identity-directory/default",
      "arn:aws:bedrock-agentcore:${data.aws_region.current.region}:${data.aws_caller_identity.current.account_id}:workload-identity-directory/default/workload-identity/*",
    ]
  }

  dynamic "statement" {
    for_each = contains([for tool in try(each.value.config.tools, []) : tool.type], "agentcore_browser") ? [1] : []

    content {
      effect = "Allow"
      actions = [
        "bedrock-agentcore:StartBrowserSession",
        "bedrock-agentcore:StopBrowserSession",
        "bedrock-agentcore:GetBrowserSession",
        "bedrock-agentcore:ListBrowserSessions",
        "bedrock-agentcore:UpdateBrowserStream",
        "bedrock-agentcore:ConnectBrowserAutomationStream",
        "bedrock-agentcore:ConnectBrowserLiveViewStream",
      ]
      resources = ["arn:aws:bedrock-agentcore:${data.aws_region.current.region}:aws:browser/*"]
    }
  }

  dynamic "statement" {
    for_each = try(each.value.config.model.api_key_arn, null) == null ? [] : [each.value.config.model.api_key_arn]

    content {
      effect = "Allow"
      actions = [
        "bedrock-agentcore:GetResourceApiKey",
      ]
      resources = [
        "arn:aws:bedrock-agentcore:${data.aws_region.current.region}:${data.aws_caller_identity.current.account_id}:token-vault/default",
        statement.value,
      ]
    }
  }

  dynamic "statement" {
    for_each = try(each.value.config.model.api_key_arn, null) == null ? [] : [each.value.config.model.api_key_arn]

    content {
      effect  = "Allow"
      actions = ["secretsmanager:GetSecretValue"]
      resources = [
        "arn:aws:secretsmanager:${data.aws_region.current.region}:${data.aws_caller_identity.current.account_id}:secret:bedrock-agentcore-identity!default/apikey/${element(split("/", statement.value), length(split("/", statement.value)) - 1)}*",
      ]
    }
  }
}

resource "aws_iam_role_policy" "agent" {
  for_each = local.agents

  name   = "${local.name_prefix}-${replace(each.key, "_", "-")}-harness"
  role   = aws_iam_role.agent[each.key].id
  policy = data.aws_iam_policy_document.agent[each.key].json
}

resource "aws_bedrockagentcore_harness" "agent" {
  for_each = local.agents

  harness_name       = each.key
  execution_role_arn = aws_iam_role.agent[each.key].arn
  timeout_seconds    = each.value.config.timeout_seconds
  max_iterations     = each.value.config.max_iterations

  environment_variables = {
    INTERVIEW_SESSIONS_BUCKET         = aws_s3_bucket.interview_sessions.bucket
    INTERVIEW_SESSION_QUESTIONS_TABLE = aws_dynamodb_table.interview_session_questions.name
  }

  model {
    litellm_model_config {
      model_id    = each.value.config.model.model_id
      api_base    = each.value.config.model.api_base
      api_key_arn = each.value.config.model.api_key_arn
    }
  }

  system_prompt {
    text = each.value.prompt
  }

  dynamic "tool" {
    for_each = try(each.value.config.tools, [])

    content {
      type = tool.value.type
      name = tool.value.name

      dynamic "config" {
        for_each = tool.value.type == "agentcore_browser" ? [1] : []

        content {
          agentcore_browser {
            browser_arn = try(tool.value.browser_arn, "arn:aws:bedrock-agentcore:${data.aws_region.current.region}:aws:browser/aws.browser.v1")
          }
        }
      }
    }
  }

  lifecycle {
    precondition {
      condition     = can(regex("^[a-zA-Z][a-zA-Z0-9_]{0,39}$", each.key))
      error_message = "Agent name must start with a letter and contain only letters, numbers, and underscores, with at most 40 characters."
    }

    precondition {
      condition     = each.value.config.model.provider == "litellm"
      error_message = "Agent ${each.key} must set model.provider to litellm."
    }

    precondition {
      condition     = can(regex("^arn:aws:bedrock-agentcore:", try(each.value.config.model.api_key_arn, "")))
      error_message = "Agent ${each.key} must set model.api_key_arn to an AgentCore Identity API key ARN."
    }
  }
}
