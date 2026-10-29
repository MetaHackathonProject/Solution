resource "aws_cognito_user_pool" "main" {
  name = "${local.name_prefix}-users"

  username_attributes = ["email"]

  username_configuration {
    case_sensitive = false
  }

  auto_verified_attributes = ["email"]

  password_policy {
    minimum_length    = 8
    require_uppercase = true
    require_lowercase = true
    require_numbers   = false
    require_symbols   = false
  }

  account_recovery_setting {
    recovery_mechanism {
      name     = "verified_email"
      priority = 1
    }
  }
}

resource "aws_cognito_user_pool_client" "web" {
  name         = "${local.name_prefix}-web"
  user_pool_id = aws_cognito_user_pool.main.id

  access_token_validity  = 12
  id_token_validity      = 12
  refresh_token_validity = 30

  token_validity_units {
    access_token  = "hours"
    id_token      = "hours"
    refresh_token = "days"
  }

  allowed_oauth_flows_user_pool_client = true
  allowed_oauth_flows                  = ["code"]
  allowed_oauth_scopes                 = ["email", "openid"]
  supported_identity_providers         = ["COGNITO"]

  callback_urls = [
    "https://${local.domains.frontend}/callback/oauth",
    "http://localhost:4200/callback/oauth",
  ]

  logout_urls = [
    "https://${local.domains.frontend}",
    "http://localhost:4200",
  ]

  explicit_auth_flows = [
    "ALLOW_USER_PASSWORD_AUTH",
    "ALLOW_REFRESH_TOKEN_AUTH",
    "ALLOW_USER_SRP_AUTH",
  ]
}

resource "aws_acm_certificate" "auth" {
  provider          = aws.us_east_1
  domain_name       = local.domains.auth
  validation_method = "DNS"
}

resource "aws_route53_record" "auth_cert_validation" {
  for_each = {
    for dvo in aws_acm_certificate.auth.domain_validation_options :
    dvo.domain_name => {
      name   = dvo.resource_record_name
      type   = dvo.resource_record_type
      record = dvo.resource_record_value
    }
  }

  zone_id = data.aws_route53_zone.main.zone_id
  name    = each.value.name
  type    = each.value.type
  ttl     = 300
  records = [each.value.record]
}

resource "aws_acm_certificate_validation" "auth" {
  provider                = aws.us_east_1
  certificate_arn         = aws_acm_certificate.auth.arn
  validation_record_fqdns = [for record in aws_route53_record.auth_cert_validation : record.fqdn]
}

resource "time_sleep" "apex_dns" {
  depends_on      = [aws_route53_record.apex]
  create_duration = "60s"
}

resource "aws_cognito_user_pool_domain" "auth" {
  domain          = local.domains.auth
  user_pool_id    = aws_cognito_user_pool.main.id
  certificate_arn = aws_acm_certificate_validation.auth.certificate_arn
  # 1 = Hosted UI (classic), 2 = managed login (required for console branding styles).
  managed_login_version = 2

  depends_on = [time_sleep.apex_dns]
}

resource "aws_route53_record" "auth" {
  zone_id = data.aws_route53_zone.main.zone_id
  name    = aws_cognito_user_pool_domain.auth.domain
  type    = "A"

  alias {
    name                   = aws_cognito_user_pool_domain.auth.cloudfront_distribution
    zone_id                = aws_cognito_user_pool_domain.auth.cloudfront_distribution_zone_id
    evaluate_target_health = false
  }
}
