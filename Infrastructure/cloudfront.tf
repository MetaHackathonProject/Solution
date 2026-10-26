locals {
  cloudfront_aliases = [
    local.domains.frontend,
  ]

  hosting_cert_sans = [
    for alias in local.cloudfront_aliases : alias if alias != local.domains.zone
  ]
}

resource "aws_cloudfront_origin_access_control" "frontend" {
  name                              = "${local.name_prefix}-s3"
  description                       = "CloudFront Distribution Origin Access Control for S3 static files server"
  origin_access_control_origin_type = "s3"
  signing_behavior                  = "always"
  signing_protocol                  = "sigv4"
}

resource "aws_acm_certificate" "hosting" {
  provider          = aws.us_east_1
  domain_name       = local.domains.zone
  validation_method = "DNS"

  subject_alternative_names = length(local.hosting_cert_sans) > 0 ? local.hosting_cert_sans : null
}

resource "aws_route53_record" "hosting_cert_validation" {
  for_each = {
    for dvo in aws_acm_certificate.hosting.domain_validation_options :
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

resource "aws_acm_certificate_validation" "hosting" {
  provider                = aws.us_east_1
  certificate_arn         = aws_acm_certificate.hosting.arn
  validation_record_fqdns = [for record in aws_route53_record.hosting_cert_validation : record.fqdn]
}

resource "aws_cloudfront_distribution" "frontend" {
  enabled             = true
  default_root_object = "index.html"
  aliases             = local.cloudfront_aliases
  price_class         = "PriceClass_All"

  origin {
    domain_name              = aws_s3_bucket.frontend.bucket_regional_domain_name
    origin_id                = aws_s3_bucket.frontend.id
    origin_access_control_id = aws_cloudfront_origin_access_control.frontend.id
  }

  default_cache_behavior {
    allowed_methods  = ["GET", "HEAD"]
    cached_methods   = ["GET", "HEAD"]
    target_origin_id = aws_s3_bucket.frontend.id

    forwarded_values {
      query_string = false

      cookies {
        forward = "none"
      }
    }

    viewer_protocol_policy = "allow-all"
    min_ttl                = 0
    default_ttl            = 3600
    max_ttl                = 86400
  }

  custom_error_response {
    error_code            = 403
    response_code         = 200
    response_page_path    = "/index.html"
    error_caching_min_ttl = 0
  }

  custom_error_response {
    error_code            = 404
    response_code         = 200
    response_page_path    = "/index.html"
    error_caching_min_ttl = 0
  }

  restrictions {
    geo_restriction {
      restriction_type = "none"
    }
  }

  viewer_certificate {
    acm_certificate_arn = aws_acm_certificate_validation.hosting.certificate_arn
    ssl_support_method  = "sni-only"
  }
}
