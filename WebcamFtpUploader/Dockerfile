FROM --platform=$TARGETPLATFORM mcr.microsoft.com/dotnet/runtime:5.0 AS base
ARG TARGETPLATFORM
ARG BUILDPLATFORM
WORKDIR /app

FROM --platform=$BUILDPLATFORM  mcr.microsoft.com/dotnet/sdk:5.0 AS build
ARG TARGETPLATFORM
ARG BUILDPLATFORM
WORKDIR /src
COPY ["WebcamFtpUploader/WebcamFtpUploader.csproj", "WebcamFtpUploader/"]
RUN dotnet restore "WebcamFtpUploader/WebcamFtpUploader.csproj"
COPY . .
WORKDIR "/src/WebcamFtpUploader"
RUN dotnet build "WebcamFtpUploader.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "WebcamFtpUploader.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "WebcamFtpUploader.dll"]
