# Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY src/SchoolAdmission.Api/SchoolAdmission.Api.csproj src/SchoolAdmission.Api/
RUN dotnet restore src/SchoolAdmission.Api/SchoolAdmission.Api.csproj
COPY src/ src/
RUN dotnet publish src/SchoolAdmission.Api/SchoolAdmission.Api.csproj -c Release -o /app --no-restore

# Tests. Only used by docker-compose.tests.yml (target: test).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS test
WORKDIR /source
COPY . .
RUN dotnet restore tests/SchoolAdmission.Api.Tests/SchoolAdmission.Api.Tests.csproj
ENTRYPOINT ["dotnet", "test", "tests/SchoolAdmission.Api.Tests/SchoolAdmission.Api.Tests.csproj", "--no-restore", "--logger", "console;verbosity=normal"]

# Run
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
# The aspnet image comes with a non-root "app" user.
USER $APP_UID
ENTRYPOINT ["dotnet", "SchoolAdmission.Api.dll"]
