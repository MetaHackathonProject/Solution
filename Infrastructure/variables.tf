variable "eleven_labs_api_key" {
  description = "ElevenLabs API key for ConvAI signed URLs (set via TF_VAR_eleven_labs_api_key / GitHub secret ELEVEN_LABS_API_KEY)"
  type        = string
  sensitive   = true
}

variable "eleven_labs_agent_id" {
  description = "ElevenLabs ConvAI agent id used for mock interviews"
  type        = string
  default     = "agent_9101m5wv7xt4e64b8mn2mpz55djr"
}

variable "eleven_labs_walkthrough_agent_id" {
  description = "ElevenLabs ConvAI agent id for post-submit solution walkthrough (no interview-questions MCP)"
  type        = string
  default     = "agent_0701m61ez7y3fnpb96fej5kcqsra"
}

variable "eleven_labs_api_url" {
  description = "ElevenLabs API base URL"
  type        = string
  default     = "https://api.elevenlabs.io"
}

variable "meta_model_api_key" {
  description = "Meta Model API key for solution vision describe (set via TF_VAR_meta_model_api_key / GitHub secret META_MODEL_API_KEY)"
  type        = string
  sensitive   = true
}

variable "meta_model_api_url" {
  description = "Meta Model API base URL"
  type        = string
  default     = "https://api.meta.ai/v1"
}

variable "meta_model_id" {
  description = "Meta model id used to describe interactive visual answers"
  type        = string
  default     = "muse-spark-1.3"
}
