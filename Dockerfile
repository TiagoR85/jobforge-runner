FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["src/JobForge.Runner/JobForge.Runner.csproj", "src/JobForge.Runner/"]
COPY ["Directory.Packages.props", "./"]
RUN dotnet restore "src/JobForge.Runner/JobForge.Runner.csproj"
COPY . .
RUN dotnet publish "src/JobForge.Runner/JobForge.Runner.csproj" -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
RUN apt-get update && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8095
EXPOSE 8095
USER $APP_UID
ENTRYPOINT ["dotnet", "JobForge.Runner.dll"]
