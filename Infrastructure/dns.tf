resource "aws_route53_zone" "main" {
  name = local.domains.zone
}
