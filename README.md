# 📒 나의 메모장 (Notepad)

에버노트 스타일의 개인 메모장 웹 앱입니다. ASP.NET Core(.NET 10) + Turso(libsql) 로 만들었습니다.

## 주요 기능
- 메모 작성 / 수정 / 삭제 (CRUD)
- 제목, 본문, 태그 관리
- 입력 시 자동 저장 (디바운스)
- 제목 / 본문 / 태그 통합 검색
- 좌측 목록 + 우측 에디터의 2단 레이아웃

## 기술 스택
- **백엔드**: ASP.NET Core Minimal API (.NET 10)
- **DB**: Turso (libsql) — HTTP(Hrana v2 pipeline) API 로 연동
- **프론트엔드**: 바닐라 HTML/CSS/JS (의존성 없음)
- **배포**: Render Blueprint (Docker, 무료 플랜)

## 로컬 실행
1. `.env` 파일에 Turso 접속 정보를 입력합니다.
   ```
   TURSO_URL=libsql://<your-db>.turso.io
   TURSO_TOKEN=<your-token>
   ```
2. 실행
   ```bash
   dotnet run
   ```
3. 브라우저에서 `http://localhost:8080` 접속

## 배포 (Render Blueprints, 무료)
1. 이 저장소를 GitHub 에 푸시합니다. (`.env` 는 `.gitignore` 로 제외됨)
2. Render 대시보드 → **New → Blueprint** 선택 후 저장소 연결
3. `render.yaml` 이 자동 인식됩니다. (`plan: free` 무료 플랜으로 설정됨)
4. 배포 과정에서 환경 변수 입력:
   - `TURSO_URL`
   - `TURSO_TOKEN`
5. 배포 완료 후 발급된 URL 로 접속

> ⚠️ `.env` 의 DB 토큰은 민감 정보이므로 절대 커밋하지 마세요. 운영 환경에서는 Render 대시보드의 환경 변수를 사용합니다.

## API
| 메서드 | 경로 | 설명 |
|--------|------|------|
| GET | `/api/notes?q=검색어` | 메모 목록 (검색 옵션) |
| GET | `/api/notes/{id}` | 단건 조회 |
| POST | `/api/notes` | 메모 생성 |
| PUT | `/api/notes/{id}` | 메모 수정 |
| DELETE | `/api/notes/{id}` | 메모 삭제 |
| GET | `/health` | 헬스 체크 |
