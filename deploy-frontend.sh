#!/bin/bash
# deploy-frontend.sh - Victoria WMS Frontend Deployment to AWS S3

# Variables (Ajustar antes de correr)
BUCKET_NAME="victoria-wms-frontend"
REGION="us-east-2"
DIST_PATH="./dist" # O ./build dependiendo del framework

echo "🚀 Iniciando despliegue de Frontend..."

# 1. Build de la aplicación
echo "📦 Compilando aplicación..."
npm install
npm run build

# 2. Sync con S3
echo "☁️ Subiendo archivos a S3 Bucket: $BUCKET_NAME"
aws s3 sync $DIST_PATH s3://$BUCKET_NAME --delete --region $REGION

# 3. Invalidar CloudFront Cache (Opcional pero recomendado)
# echo "🧹 Limpiando cache de CloudFront..."
# DISTRIBUTION_ID=$(aws cloudfront list-distributions --query "DistributionSummaryList[?Aliases.Items!=null && contains(Aliases.Items, 'victoriawms.dev')].Id" --output text)
# aws cloudfront create-invalidation --distribution-id $DISTRIBUTION_ID --paths "/*"

echo "✅ Despliegue completado con éxito. Visita https://victoriawms.dev"
