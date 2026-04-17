# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081


# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
ARG VERSION=0.0.0-dev
ARG ASSEMBLY_VERSION=0.0.0.0
ARG FILE_VERSION=0.0.0.0
ARG INFORMATIONAL_VERSION=0.0.0-dev
WORKDIR /src
COPY ["Directory.Build.targets", "./"]
COPY ["Directory.Build.props", "./"]
COPY ["version.json", "./"]
COPY ["src/CPCA.MailMule.Application/", "src/CPCA.MailMule.Application/"]
COPY ["src/CPCA.MailMule.Application.Contracts/", "src/CPCA.MailMule.Application.Contracts/"]
COPY ["src/CPCA.MailMule.Backend/", "src/CPCA.MailMule.Backend/"]
COPY ["src/CPCA.MailMule.Core/", "src/CPCA.MailMule.Core/"]
COPY ["src/CPCA.MailMule.Domain/", "src/CPCA.MailMule.Domain/"]
COPY ["src/CPCA.MailMule.Domain.Shared/", "src/CPCA.MailMule.Domain.Shared/"]
COPY ["src/CPCA.MailMule.Persistence/", "src/CPCA.MailMule.Persistence/"]
COPY ["src/CPCA.MailMule.Persistence.PostgreSql/", "src/CPCA.MailMule.Persistence.PostgreSql/"]
COPY ["src/CPCA.MailMule.Shared/", "src/CPCA.MailMule.Shared/"]
COPY ["aspire/CPCA.MailMule.ServiceDefaults/", "aspire/CPCA.MailMule.ServiceDefaults/"]
RUN dotnet restore "./src/CPCA.MailMule.Backend/CPCA.MailMule.Backend.csproj"
COPY . .
WORKDIR "/src/src/CPCA.MailMule.Backend"
RUN dotnet build "./CPCA.MailMule.Backend.csproj" -c $BUILD_CONFIGURATION -o /app/build /p:ContainerBuildVersion=$VERSION /p:ContainerAssemblyVersion=$ASSEMBLY_VERSION /p:ContainerFileVersion=$FILE_VERSION /p:ContainerInformationalVersion=$INFORMATIONAL_VERSION

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
ARG VERSION=0.0.0-dev
ARG ASSEMBLY_VERSION=0.0.0.0
ARG FILE_VERSION=0.0.0.0
ARG INFORMATIONAL_VERSION=0.0.0-dev
RUN dotnet publish "./CPCA.MailMule.Backend.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false /p:ContainerBuildVersion=$VERSION /p:ContainerAssemblyVersion=$ASSEMBLY_VERSION /p:ContainerFileVersion=$FILE_VERSION /p:ContainerInformationalVersion=$INFORMATIONAL_VERSION

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
ARG APP_VERSION=0.0.0-dev
WORKDIR /app
LABEL org.opencontainers.image.version=$APP_VERSION
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "CPCA.MailMule.Backend.dll"]
