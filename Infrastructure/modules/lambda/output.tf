output "function_name" {
  value = aws_lambda_function.lambda_function.function_name
}

output "function_arn" {
  value = aws_lambda_function.lambda_function.arn
}

output "role_name" {
  value = aws_iam_role.role.name
}

output "invoke_arn" {
  value = aws_lambda_function.lambda_function.invoke_arn
}
