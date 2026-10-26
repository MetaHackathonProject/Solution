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
