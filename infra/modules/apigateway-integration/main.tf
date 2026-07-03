locals {
  common_tags = merge(var.tags, { Module = "apigateway-integration" })
  public_routes = {
    for route_key in var.public_route_keys : route_key => "public"
  }
  protected_routes = {
    for route_key in var.protected_route_keys : route_key => "protected"
  }
  routes = merge(local.public_routes, local.protected_routes)
  protected_request_parameters = {
    "overwrite:header.X-Platform-User-Id"      = "$context.authorizer.userId"
    "overwrite:header.X-Platform-Email"        = "$context.authorizer.email"
    "overwrite:header.X-Platform-Roles"        = "$context.authorizer.roles"
    "overwrite:header.X-Platform-Proxy-Secret" = "$context.authorizer.proxySecret"
  }
  use_tag_lookup = length(var.alb_discovery_tags) > 0
  alb_arn        = var.enabled ? (local.use_tag_lookup ? data.aws_lb.ingress_by_tags[0].arn : data.aws_lb.ingress_by_name[0].arn) : null
}

data "aws_lb" "ingress_by_tags" {
  count = var.enabled && local.use_tag_lookup ? 1 : 0
  tags  = var.alb_discovery_tags
}

data "aws_lb" "ingress_by_name" {
  count = var.enabled && !local.use_tag_lookup ? 1 : 0
  name  = var.alb_name
}

data "aws_lb_listener" "ingress" {
  count             = var.enabled ? 1 : 0
  load_balancer_arn = local.alb_arn
  port              = var.alb_listener_port
}

resource "aws_apigatewayv2_integration" "public" {
  count                  = var.enabled ? 1 : 0
  api_id                 = var.api_id
  integration_type       = "HTTP_PROXY"
  integration_method     = "ANY"
  integration_uri        = data.aws_lb_listener.ingress[0].arn
  connection_type        = "VPC_LINK"
  connection_id          = var.vpc_link_id
  payload_format_version = "1.0"
  timeout_milliseconds   = 29000
  description            = "Public ALB integration for unauthenticated auth routes."
}

resource "aws_apigatewayv2_integration" "protected" {
  count                  = var.enabled ? 1 : 0
  api_id                 = var.api_id
  integration_type       = "HTTP_PROXY"
  integration_method     = "ANY"
  integration_uri        = data.aws_lb_listener.ingress[0].arn
  connection_type        = "VPC_LINK"
  connection_id          = var.vpc_link_id
  payload_format_version = "1.0"
  timeout_milliseconds   = 29000
  description            = "Protected ALB integration that injects API-Gateway-verified identity headers."
  request_parameters     = local.protected_request_parameters
}

resource "aws_apigatewayv2_route" "this" {
  for_each = var.enabled ? local.routes : {}

  api_id    = var.api_id
  route_key = each.key
  target = format(
    "integrations/%s",
  each.value == "protected" ? aws_apigatewayv2_integration.protected[0].id : aws_apigatewayv2_integration.public[0].id)
  authorization_type = each.value == "protected" ? "CUSTOM" : "NONE"
  authorizer_id      = each.value == "protected" ? var.authorizer_id : null
}
