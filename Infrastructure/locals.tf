locals {
  env         = yamldecode(file("${path.module}/env/hackathon.yml"))
  name_prefix = local.env.name_prefix

  domains = {
    zone     = local.env.domain
    frontend = local.env.domain
    api      = "api.${local.env.domain}"
    auth     = "auth.${local.env.domain}"
  }
}
