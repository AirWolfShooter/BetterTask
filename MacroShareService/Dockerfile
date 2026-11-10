FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY MacroShareService/MacroShareService.csproj MacroShareService/
RUN dotnet restore MacroShareService/MacroShareService.csproj
COPY MacroShareService/ MacroShareService/
WORKDIR /src/MacroShareService
RUN dotnet publish MacroShareService.csproj --configuration Release --output /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./
ENV ASPNETCORE_HTTP_PORTS=10000
EXPOSE 10000
ENTRYPOINT ["dotnet", "MacroShareService.dll"]
