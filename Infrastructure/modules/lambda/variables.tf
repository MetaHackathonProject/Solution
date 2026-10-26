variable "lambdas_bucket_id" {
  description = "ID of the S3 bucket that stores Lambda deployment packages."
  type        = string
}

variable "handler" {
  description = "Lambda handler. Required for zip packages."
  type        = string
  default     = ""
}

variable "environment" {
  type    = map(string)
  default = {}
}

variable "name_prefix" {
  type = string
}

variable "env" {
  type = string
}

variable "image_based" {
  type    = bool
  default = false
}

variable "image_arn" {
  type    = string
  default = ""
}

variable "layers" {
  type    = list(string)
  default = []
}

variable "architecture" {
  type    = string
  default = "x86_64"
}

variable "runtime" {
  type    = string
  default = "dotnet10"
}

variable "memory" {
  type    = number
  default = 256
}

variable "timeout" {
  type    = number
  default = 30
}
