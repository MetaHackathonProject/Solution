data "aws_route53_zone" "main" {
  name         = local.domains.zone
  private_zone = false
}
