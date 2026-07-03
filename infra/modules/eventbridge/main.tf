locals {
  common_tags = merge(var.tags, { Module = "eventbridge" })
}

resource "aws_cloudwatch_event_bus" "this" {
  name = var.event_bus_name

  tags = merge(local.common_tags, {
    Name = var.event_bus_name
  })
}

resource "aws_cloudwatch_event_rule" "this" {
  for_each = var.rules

  name           = "${var.event_bus_name}-${each.key}"
  description    = each.value.description
  event_bus_name = aws_cloudwatch_event_bus.this.name
  event_pattern  = each.value.event_pattern

  tags = merge(local.common_tags, {
    Name = "${var.event_bus_name}-${each.key}"
  })
}

resource "aws_cloudwatch_event_target" "sqs" {
  for_each = var.rules

  rule           = aws_cloudwatch_event_rule.this[each.key].name
  event_bus_name = aws_cloudwatch_event_bus.this.name
  arn            = each.value.target_queue_arn
  input_path     = each.value.input_path
  target_id      = "${each.key}-sqs"
}

data "aws_iam_policy_document" "queue_policy" {
  for_each = var.rules

  statement {
    effect = "Allow"

    actions = ["sqs:SendMessage"]

    resources = [each.value.target_queue_arn]

    principals {
      type        = "Service"
      identifiers = ["events.amazonaws.com"]
    }

    condition {
      test     = "ArnEquals"
      variable = "aws:SourceArn"
      values   = [aws_cloudwatch_event_rule.this[each.key].arn]
    }
  }
}

resource "aws_sqs_queue_policy" "this" {
  for_each = var.rules

  queue_url = each.value.target_queue_url
  policy    = data.aws_iam_policy_document.queue_policy[each.key].json
}
