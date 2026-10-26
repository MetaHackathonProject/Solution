data "aws_route53_zone" "main" {
  name         = local.domains.zone
  private_zone = false
}

# Cognito rejects a custom domain unless the parent name has a public A record.
resource "aws_route53_record" "apex" {
  zone_id = data.aws_route53_zone.main.zone_id
  name    = local.domains.zone
  type    = "A"
  ttl     = 300
  records = ["192.0.2.1"]
}
