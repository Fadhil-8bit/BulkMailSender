# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

# Set environment variables for large file uploads and HTTP-only mode
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_Kestrel__Limits__MaxRequestBodySize=524288000
ENV ASPNETCORE_Kestrel__Limits__KeepAliveTimeout=00:10:00


# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["BulkMailSender/BulkMailSender.csproj", "BulkMailSender/"]
RUN dotnet restore "./BulkMailSender/BulkMailSender.csproj"
COPY . .
WORKDIR "/src/BulkMailSender"
RUN dotnet build "./BulkMailSender.csproj" -c Release -o /app/build

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
RUN dotnet publish "./BulkMailSender.csproj" -c Release -o /app/publish /p:UseAppHost=false

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Create uploads directory with proper permissions and switch to app user
USER root
RUN mkdir -p /app/wwwroot/uploads && chown -R app:app /app/wwwroot/uploads
USER app

ENTRYPOINT ["dotnet", "BulkMailSender.dll"]