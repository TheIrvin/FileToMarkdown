#!/usr/bin/env bash
set -euo pipefail

# === Azure Deployment Script for MarkItDownWeb ===
# Adjust variables below before running.

RESOURCE_GROUP="rg-markitdownweb"
LOCATION="eastus"
APP_SERVICE_PLAN="plan-markitdownweb"
WEB_APP_NAME="markitdownweb-app"
SQL_SERVER="sql-markitdownweb"
SQL_DB="MarkItDownWeb"
SQL_ADMIN="sqladmin"
SQL_PASSWORD="YourStrongPassword123!"

az login

# Resource group
az group create --name "$RESOURCE_GROUP" --location "$LOCATION"

# App Service Plan + Web App
az appservice plan create --name "$APP_SERVICE_PLAN" --resource-group "$RESOURCE_GROUP" --sku B1 --is-linux
az webapp create --resource-group "$RESOURCE_GROUP" --plan "$APP_SERVICE_PLAN" \
  --name "$WEB_APP_NAME" --runtime "DOTNETCORE:9.0"

# SQL Server + Database
az sql server create --name "$SQL_SERVER" --resource-group "$RESOURCE_GROUP" \
  --admin-user "$SQL_ADMIN" --admin-password "$SQL_PASSWORD"
az sql db create --resource-group "$RESOURCE_GROUP" --server "$SQL_SERVER" \
  --name "$SQL_DB" --service-objective S0

# Connection string
CS="Server=tcp:$SQL_SERVER.database.windows.net,1433;Database=$SQL_DB;User ID=$SQL_ADMIN;Password=$SQL_PASSWORD;Trusted_Connection=False;Encrypt=True;"
az webapp config connection-string set --resource-group "$RESOURCE_GROUP" \
  --name "$WEB_APP_NAME" --settings DefaultConnection="$CS" --connection-string-type SQLAzure

# App settings
az webapp config appsettings set --resource-group "$RESOURCE_GROUP" --name "$WEB_APP_NAME" \
  --settings MarkItDown__PythonPath="/usr/bin/python3" Storage__UploadsFolder="/home/site/wwwroot/Uploads"

# Publish and deploy
dotnet publish src/Web -c Release -o ./publish
cd publish && zip -r ../publish.zip . && cd ..
az webapp deploy --resource-group "$RESOURCE_GROUP" --name "$WEB_APP_NAME" --src-path publish.zip --type zip
