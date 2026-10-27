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

variable "eleven_labs_api_url" {
  description = "ElevenLabs API base URL"
  type        = string
  default     = "https://api.elevenlabs.io"
}
