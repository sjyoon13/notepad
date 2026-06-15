using Notepad.Models;

namespace Notepad.Services;

// 메모 데이터에 대한 CRUD 및 검색을 담당하는 저장소 클래스
public class NoteRepository
{
    private readonly TursoClient _db;

    public NoteRepository(TursoClient db)
    {
        _db = db;
    }

    // 앱 시작 시 notes 테이블이 없으면 생성한다.
    public async Task EnsureSchemaAsync()
    {
        try
        {
            await _db.ExecuteAsync(@"
                CREATE TABLE IF NOT EXISTS notes (
                    id         INTEGER PRIMARY KEY AUTOINCREMENT,
                    title      TEXT NOT NULL DEFAULT '',
                    content    TEXT NOT NULL DEFAULT '',
                    tags       TEXT NOT NULL DEFAULT '',
                    created_at TEXT NOT NULL,
                    updated_at TEXT NOT NULL
                )");
        }
        catch (Exception ex)
        {
            throw new TursoException($"스키마 초기화 실패: {ex.Message}", ex);
        }
    }

    // 메모 목록 조회. search 가 있으면 제목/본문/태그에서 부분 검색한다.
    public async Task<List<Note>> GetAllAsync(string? search = null)
    {
        TursoResult result;
        if (string.IsNullOrWhiteSpace(search))
        {
            result = await _db.ExecuteAsync(
                "SELECT * FROM notes ORDER BY updated_at DESC");
        }
        else
        {
            var keyword = $"%{search.Trim()}%";
            result = await _db.ExecuteAsync(
                @"SELECT * FROM notes
                  WHERE title LIKE ? OR content LIKE ? OR tags LIKE ?
                  ORDER BY updated_at DESC",
                keyword, keyword, keyword);
        }

        return result.Rows.Select(MapNote).ToList();
    }

    // 단건 조회. 없으면 null 반환.
    public async Task<Note?> GetByIdAsync(long id)
    {
        var result = await _db.ExecuteAsync("SELECT * FROM notes WHERE id = ?", id);
        return result.Rows.Count == 0 ? null : MapNote(result.Rows[0]);
    }

    // 새 메모 생성. 생성된 메모를 반환한다.
    public async Task<Note> CreateAsync(Note note)
    {
        var now = DateTime.UtcNow.ToString("o"); // ISO-8601
        var result = await _db.ExecuteAsync(
            @"INSERT INTO notes (title, content, tags, created_at, updated_at)
              VALUES (?, ?, ?, ?, ?)",
            note.Title, note.Content, note.Tags, now, now);

        note.Id = result.LastInsertRowId;
        note.CreatedAt = now;
        note.UpdatedAt = now;
        return note;
    }

    // 기존 메모 수정. 성공 여부 반환.
    public async Task<bool> UpdateAsync(long id, Note note)
    {
        var now = DateTime.UtcNow.ToString("o");
        var result = await _db.ExecuteAsync(
            @"UPDATE notes
              SET title = ?, content = ?, tags = ?, updated_at = ?
              WHERE id = ?",
            note.Title, note.Content, note.Tags, now, id);

        return result.AffectedRows > 0;
    }

    // 메모 삭제. 성공 여부 반환.
    public async Task<bool> DeleteAsync(long id)
    {
        var result = await _db.ExecuteAsync("DELETE FROM notes WHERE id = ?", id);
        return result.AffectedRows > 0;
    }

    // DB 행(딕셔너리)을 Note 객체로 매핑한다.
    private static Note MapNote(Dictionary<string, object?> row) => new()
    {
        Id = row["id"] is long l ? l : 0,
        Title = row["title"]?.ToString() ?? "",
        Content = row["content"]?.ToString() ?? "",
        Tags = row["tags"]?.ToString() ?? "",
        CreatedAt = row["created_at"]?.ToString() ?? "",
        UpdatedAt = row["updated_at"]?.ToString() ?? ""
    };
}
