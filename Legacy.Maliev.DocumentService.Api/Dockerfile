# syntax=docker/dockerfile:1.7
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
ENV GITHUB_ACTIONS=false
COPY nuget.config .
COPY Directory.Build.props .
COPY .dependencies/ .dependencies/
COPY Legacy.Maliev.DocumentService.Api/Legacy.Maliev.DocumentService.Api.csproj Legacy.Maliev.DocumentService.Api/
COPY Legacy.Maliev.DocumentService.Application/Legacy.Maliev.DocumentService.Application.csproj Legacy.Maliev.DocumentService.Application/
COPY Legacy.Maliev.DocumentService.Domain/Legacy.Maliev.DocumentService.Domain.csproj Legacy.Maliev.DocumentService.Domain/
COPY Legacy.Maliev.DocumentService.Rendering/Legacy.Maliev.DocumentService.Rendering.csproj Legacy.Maliev.DocumentService.Rendering/
RUN dotnet restore Legacy.Maliev.DocumentService.Api/Legacy.Maliev.DocumentService.Api.csproj \
    -p:MalievWorkspaceRoot=/src/.dependencies
COPY . .
RUN dotnet publish Legacy.Maliev.DocumentService.Api/Legacy.Maliev.DocumentService.Api.csproj \
    --configuration Release --no-restore --output /app /p:UseAppHost=false \
    -p:MalievWorkspaceRoot=/src/.dependencies

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build --chown=$APP_UID:$APP_UID /app .
USER $APP_UID
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "Legacy.Maliev.DocumentService.Api.dll"]
