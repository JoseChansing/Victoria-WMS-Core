#!/bin/bash
# Script de Despliegue Frontend - Victoria WMS

# Variables
BUCKET_NAME="app.victoriawms.dev"
DIST_DIR="./src/Victoria.UI/dist"
DISTRIBUTION_ID="TU_DISTRIBUTION_ID_AQUÍ"

echo "🚀 Iniciando despliegue de Frontend..."

# 1. Compilar aplicación
# cd src/Victoria.UI
# npm install
# npm run build
# cd ../..

# 2. Sincronizar con S3
echo "📦 Subiendo archivos a S3..."
aws s3 sync $DIST_DIR s3://$BUCKET_NAME --delete --acl public-read

# 3. Invalidar Caché de CloudFront
echo "🛡️ Invalidando caché de CloudFront..."
aws cloudfront create-invalidation --distribution-id $DISTRIBUTION_ID --paths "/*"

echo "✅ Despliegue completado con éxito en https://app.victoriawms.dev"
