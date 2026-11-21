FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore first, so the packages are cached when only the code changes.
COPY OrgMind.API.csproj ./
RUN dotnet restore OrgMind.API.csproj

COPY . .
RUN dotnet publish OrgMind.API.csproj -c Release -o /out --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /out .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "OrgMind.API.dll"]