FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY EasySwitcher/EasySwitcher.csproj EasySwitcher/
RUN dotnet restore EasySwitcher/EasySwitcher.csproj
COPY . .
WORKDIR /src/EasySwitcher
ARG TARGETARCH
RUN if [ "$TARGETARCH" = "arm64" ]; then RID="linux-arm64"; else RID="linux-x64"; fi; \
    dotnet publish -c Release -r $RID --self-contained true -p:PublishAot=false -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://0.0.0.0:7085
ENTRYPOINT ["./EasySwitcher"]
