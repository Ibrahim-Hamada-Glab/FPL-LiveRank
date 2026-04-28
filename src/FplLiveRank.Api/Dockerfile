FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/FplLiveRank.Api/FplLiveRank.Api.csproj src/FplLiveRank.Api/
COPY src/FplLiveRank.Application/FplLiveRank.Application.csproj src/FplLiveRank.Application/
COPY src/FplLiveRank.Domain/FplLiveRank.Domain.csproj src/FplLiveRank.Domain/
COPY src/FplLiveRank.Infrastructure/FplLiveRank.Infrastructure.csproj src/FplLiveRank.Infrastructure/
RUN dotnet restore src/FplLiveRank.Api/FplLiveRank.Api.csproj

COPY src/ src/
RUN dotnet publish src/FplLiveRank.Api/FplLiveRank.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "FplLiveRank.Api.dll"]
