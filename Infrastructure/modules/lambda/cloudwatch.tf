resource "aws_cloudwatch_log_group" "logs" {
  name              = "/aws/lambda/${var.name_prefix}-${var.env}"
  retention_in_days = 5
}
