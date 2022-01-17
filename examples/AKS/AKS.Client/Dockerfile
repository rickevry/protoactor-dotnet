FROM mcr.microsoft.com/dotnet/runtime:6.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["AKS.Client/AKS.Client.csproj", "AKS.Client/"]
COPY ["AKS.Shared/AKS.Shared.csproj", "AKS.Shared/"]
RUN dotnet restore "AKS.Client/AKS.Client.csproj"
COPY . .
WORKDIR "/src/AKS.Client"
RUN dotnet build "AKS.Client.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "AKS.Client.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "AKS.Client.dll"]