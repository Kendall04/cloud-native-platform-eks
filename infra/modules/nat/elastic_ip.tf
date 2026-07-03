resource "aws_eip" "nat" {
  domain = "vpc"
  tags = merge(var.tags, {
    "Name" = "${var.name_prefix}-nat-eip"
  })
}

resource "aws_eip_association" "nat" {
  instance_id   = aws_instance.nat.id
  allocation_id = aws_eip.nat.id
}
