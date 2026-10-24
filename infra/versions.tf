terraform {
  required_version = ">= 1.5.0"

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.0"
    }
  }

  # Estado remoto no S3. Bucket, key e regiao sao passados no `terraform init`
  # (-backend-config), para o mesmo codigo servir homolog e prod.
  backend "s3" {}
}

provider "aws" {
  region = var.aws_region

  default_tags {
    tags = {
      Project     = var.project
      Environment = var.environment
      Repository  = "oficina-lambda-auth"
      ManagedBy   = "terraform"
    }
  }
}
