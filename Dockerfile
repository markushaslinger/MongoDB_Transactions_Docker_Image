FROM mongo:6.0.3-focal AS base
EXPOSE 27017

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY ["MongoControl/MongoControl.csproj", "MongoControl/"]
RUN dotnet restore "MongoControl/MongoControl.csproj"
COPY . .
WORKDIR "/src/MongoControl"
RUN dotnet build "MongoControl.csproj" -c Release -r linux-x64 -o /app/build -p:PublishAot=true

FROM build AS publish
RUN dotnet publish "MongoControl.csproj" -c Release -r linux-x64 -o /app/publish -p:PublishAot=true


FROM base AS final
WORKDIR /mongo
COPY --from=publish /app/publish .
RUN chmod +x MongoControl
ENTRYPOINT ["/mongo/MongoControl"]
