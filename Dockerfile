# 1) Build the React app
FROM node:22-alpine AS web
WORKDIR /web
COPY web/package*.json ./
RUN npm ci
COPY web/ ./
ARG VITE_AG_GRID_LICENSE_KEY
ENV VITE_AG_GRID_LICENSE_KEY=$VITE_AG_GRID_LICENSE_KEY
RUN npm run build

# 2) Build the .NET API
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api
WORKDIR /src
COPY . .
RUN dotnet publish src/Tally.Api/Tally.Api.csproj -c Release -o /app

# 3) Run: one small image with the API and the React files
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=api /app ./
COPY --from=web /web/dist ./wwwroot
ENV ASPNETCORE_HTTP_PORTS=10000
EXPOSE 10000
ENTRYPOINT ["dotnet", "Tally.Api.dll"]