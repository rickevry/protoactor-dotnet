FROM mcr.microsoft.com/dotnet/runtime:6.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["AKS.Server/AKS.Server.csproj", "AKS.Server/"]
COPY ["AKS.Shared/AKS.Shared.csproj", "AKS.Shared/"]
RUN dotnet restore "AKS.Server/AKS.Server.csproj"
COPY . .
WORKDIR "/src/AKS.Server"
RUN dotnet build "AKS.Server.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "AKS.Server.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "AKS.Server.dll"]