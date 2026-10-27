output "environment" {
  value = local.env.environment
}

output "domains" {
  value = local.domains
}

output "name_servers" {
  value = data.aws_route53_zone.main.name_servers
}

output "user_pool_id" {
  value = aws_cognito_user_pool.main.id
}

output "user_pool_client_id" {
  value = aws_cognito_user_pool_client.web.id
}

output "auth_url" {
  value = "https://${local.domains.auth}"
}

output "frontend_bucket" {
  value = aws_s3_bucket.frontend.bucket
}

output "cloudfront_distribution_id" {
  value = aws_cloudfront_distribution.frontend.id
}

output "cloudfront_domain_name" {
  value = aws_cloudfront_distribution.frontend.domain_name
}

output "api_url" {
  value = "https://${local.domains.api}"
}

output "api_function_name" {
  value = module.api.function_name
}

output "interview_sessions_bucket" {
  value = aws_s3_bucket.interview_sessions.bucket
}

output "interview_sessions_table" {
  value = aws_dynamodb_table.interview_sessions.name
}

output "interview_planner_state_machine_arn" {
  value = aws_sfn_state_machine.interview_planner.arn
}

output "interview_session_questions_table" {
  value = aws_dynamodb_table.interview_session_questions.name
}

output "interview_sessions_progress_table" {
  value = aws_dynamodb_table.interview_sessions_progress.name
}

output "seed_interview_progress_function_name" {
  value = module.seed_interview_progress.function_name
}

output "seed_interview_progress_function_arn" {
  value = module.seed_interview_progress.function_arn
}

output "agent_harnesses" {
  value = {
    for name, harness in aws_bedrockagentcore_harness.agent : name => harness.arn
  }
}
