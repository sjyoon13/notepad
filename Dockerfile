# ---------- 빌드 단계 ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# 의존성 복원 (캐시 활용을 위해 csproj 먼저 복사)
COPY notepad.csproj ./
RUN dotnet restore

# 전체 소스 복사 후 게시
COPY . ./
RUN dotnet publish notepad.csproj -c Release -o /app

# ---------- 실행 단계 ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app ./

# Render 는 PORT 환경 변수(기본 10000)로 트래픽을 전달한다.
EXPOSE 10000
ENTRYPOINT ["dotnet", "notepad.dll"]
