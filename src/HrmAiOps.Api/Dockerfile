FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY src/HrmAiOps.Domain/HrmAiOps.Domain.csproj src/HrmAiOps.Domain/
COPY src/HrmAiOps.Application/HrmAiOps.Application.csproj src/HrmAiOps.Application/
COPY src/HrmAiOps.Infrastructure/HrmAiOps.Infrastructure.csproj src/HrmAiOps.Infrastructure/
COPY src/HrmAiOps.Api/HrmAiOps.Api.csproj src/HrmAiOps.Api/
RUN dotnet restore src/HrmAiOps.Api/HrmAiOps.Api.csproj
COPY . .
RUN dotnet publish src/HrmAiOps.Api/HrmAiOps.Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "HrmAiOps.Api.dll"]
