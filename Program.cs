using Notepad.Models;
using Notepad.Services;

var builder = WebApplication.CreateBuilder(args);

// 로컬 개발 환경에서 .env 파일이 있으면 환경 변수로 읽어들인다.
// (운영 환경인 Render 에서는 대시보드의 환경 변수가 사용된다.)
LoadDotEnv(Path.Combine(builder.Environment.ContentRootPath, ".env"));
builder.Configuration.AddEnvironmentVariables();

// Render 는 PORT 환경 변수로 수신 포트를 지정한다. 없으면 8080 사용(로컬).
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

// 서비스 등록 (의존성 주입)
builder.Services.AddHttpClient<TursoClient>();          // HttpClient 기반 Turso 클라이언트
builder.Services.AddScoped<NoteRepository>();           // 메모 저장소

var app = builder.Build();

// 앱 시작 시 테이블 스키마 보장
try
{
    using var scope = app.Services.CreateScope();
    var repo = scope.ServiceProvider.GetRequiredService<NoteRepository>();
    await repo.EnsureSchemaAsync();
}
catch (Exception ex)
{
    // 초기화 실패해도 앱은 기동시키되, 로그로 원인을 남긴다.
    app.Logger.LogError(ex, "데이터베이스 스키마 초기화에 실패했습니다.");
}

// 정적 파일(프론트엔드) 제공: wwwroot/index.html
app.UseDefaultFiles();
app.UseStaticFiles();

// 헬스 체크 (Render 헬스체크 경로)
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// ---------- 메모 REST API ----------

// 목록 조회 (검색어 q 옵션)
app.MapGet("/api/notes", async (string? q, NoteRepository repo) =>
{
    try
    {
        var notes = await repo.GetAllAsync(q);
        return Results.Ok(notes);
    }
    catch (Exception ex)
    {
        return Results.Problem($"메모 목록 조회 실패: {ex.Message}");
    }
});

// 단건 조회
app.MapGet("/api/notes/{id:long}", async (long id, NoteRepository repo) =>
{
    try
    {
        var note = await repo.GetByIdAsync(id);
        return note is null ? Results.NotFound(new { message = "메모를 찾을 수 없습니다." }) : Results.Ok(note);
    }
    catch (Exception ex)
    {
        return Results.Problem($"메모 조회 실패: {ex.Message}");
    }
});

// 생성
app.MapPost("/api/notes", async (Note note, NoteRepository repo) =>
{
    try
    {
        var created = await repo.CreateAsync(note);
        return Results.Created($"/api/notes/{created.Id}", created);
    }
    catch (Exception ex)
    {
        return Results.Problem($"메모 생성 실패: {ex.Message}");
    }
});

// 수정
app.MapPut("/api/notes/{id:long}", async (long id, Note note, NoteRepository repo) =>
{
    try
    {
        var ok = await repo.UpdateAsync(id, note);
        return ok ? Results.NoContent() : Results.NotFound(new { message = "메모를 찾을 수 없습니다." });
    }
    catch (Exception ex)
    {
        return Results.Problem($"메모 수정 실패: {ex.Message}");
    }
});

// 삭제
app.MapDelete("/api/notes/{id:long}", async (long id, NoteRepository repo) =>
{
    try
    {
        var ok = await repo.DeleteAsync(id);
        return ok ? Results.NoContent() : Results.NotFound(new { message = "메모를 찾을 수 없습니다." });
    }
    catch (Exception ex)
    {
        return Results.Problem($"메모 삭제 실패: {ex.Message}");
    }
});

app.Run();

// .env 파일을 읽어 환경 변수로 설정하는 헬퍼 (KEY=VALUE 형식)
static void LoadDotEnv(string path)
{
    try
    {
        if (!File.Exists(path)) return;

        foreach (var rawLine in File.ReadAllLines(path))
        {
            var line = rawLine.Trim();
            // 빈 줄이나 주석(#)은 건너뛴다.
            if (line.Length == 0 || line.StartsWith('#')) continue;

            var idx = line.IndexOf('=');
            if (idx <= 0) continue;

            var key = line[..idx].Trim();
            var value = line[(idx + 1)..].Trim().Trim('"');

            // 이미 설정된 환경 변수는 덮어쓰지 않는다.
            if (Environment.GetEnvironmentVariable(key) is null)
                Environment.SetEnvironmentVariable(key, value);
        }
    }
    catch
    {
        // .env 로딩 실패는 치명적이지 않으므로 무시한다.
    }
}
