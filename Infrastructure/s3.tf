resource "aws_s3_bucket" "lambdas" {
  bucket = "${local.name_prefix}-lambdas-bucket"
}

resource "aws_s3_bucket_versioning" "lambdas" {
  bucket = aws_s3_bucket.lambdas.id

  versioning_configuration {
    status = "Enabled"
  }
}

resource "aws_s3_bucket_public_access_block" "lambdas" {
  bucket                  = aws_s3_bucket.lambdas.id
  block_public_acls       = true
  block_public_policy     = true
  ignore_public_acls      = true
  restrict_public_buckets = true
}

resource "aws_s3_bucket" "interview_sessions" {
  bucket = "${local.name_prefix}-interview-sessions-bucket"
}

resource "aws_s3_bucket_public_access_block" "interview_sessions" {
  bucket                  = aws_s3_bucket.interview_sessions.id
  block_public_acls       = true
  block_public_policy     = true
  ignore_public_acls      = true
  restrict_public_buckets = true
}