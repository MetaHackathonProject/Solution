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
