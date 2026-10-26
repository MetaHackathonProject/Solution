terraform {
  required_version = ">= 1.10"

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 6.45"
    }
  }

  backend "s3" {}
}

provider "aws" {
  region = local.env.region

  default_tags {
    tags = {
      project     = local.env.name_prefix
      environment = local.env.environment
    }
  }
}
