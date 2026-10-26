output "environment" {
  value = local.env.environment
}

output "domains" {
  value = local.domains
}

output "name_servers" {
  value = aws_route53_zone.main.name_servers
}
