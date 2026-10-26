data "aws_route53_zone" "main" {
  name         = local.domains.zone
  private_zone = false
}

# Public A record for the site. Cognito also requires this parent name to resolve.
resource "aws_route53_record" "apex" {
  zone_id = data.aws_route53_zone.main.zone_id
  name    = local.domains.frontend
  type    = "A"

  alias {
    name                   = aws_cloudfront_distribution.frontend.domain_name
    zone_id                = aws_cloudfront_distribution.frontend.hosted_zone_id
    evaluate_target_health = false
  }
}
