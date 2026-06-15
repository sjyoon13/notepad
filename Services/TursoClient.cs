using System.Text;
using System.Text.Json;

namespace Notepad.Services;

// Turso(libsql) 데이터베이스와 HTTP(Hrana v2 pipeline) 프로토콜로 통신하는 저수준 클라이언트
// 별도의 드라이버 없이 HttpClient 만으로 SQL 을 실행한다.
public class TursoClient
{
    private readonly HttpClient _http;
    private readonly string _pipelineUrl;
    private readonly string _token;

    public TursoClient(HttpClient http, IConfiguration config)
    {
        _http = http;

        // 환경 변수(.env 또는 Render 환경 변수)에서 접속 정보를 읽는다.
        var url = config["TURSO_URL"]
            ?? throw new InvalidOperationException("TURSO_URL 환경 변수가 설정되지 않았습니다.");
        _token = config["TURSO_TOKEN"]
            ?? throw new InvalidOperationException("TURSO_TOKEN 환경 변수가 설정되지 않았습니다.");

        // libsql:// 스킴을 HTTP API 용 https:// 로 변환한다.
        var httpUrl = url
            .Replace("libsql://", "https://", StringComparison.OrdinalIgnoreCase)
            .TrimEnd('/');
        _pipelineUrl = $"{httpUrl}/v2/pipeline";
    }

    // SQL 한 문장을 실행하고 결과(컬럼/행)를 반환한다.
    // args 는 SQL 의 ? 자리표시자에 순서대로 바인딩된다.
    public async Task<TursoResult> ExecuteAsync(string sql, params object?[] args)
    {
        try
        {
            // Hrana pipeline 요청 본문 구성: execute 후 close
            var requestBody = new
            {
                requests = new object[]
                {
                    new
                    {
                        type = "execute",
                        stmt = new
                        {
                            sql,
                            args = args.Select(BuildArg).ToArray()
                        }
                    },
                    new { type = "close" }
                }
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, _pipelineUrl);
            request.Headers.Add("Authorization", $"Bearer {_token}");
            request.Content = new StringContent(
                JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

            using var response = await _http.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new TursoException($"Turso 요청 실패 ({(int)response.StatusCode}): {json}");

            return ParseResult(json);
        }
        catch (TursoException)
        {
            throw; // 이미 가공된 예외는 그대로 전달
        }
        catch (Exception ex)
        {
            // 네트워크/직렬화 등 예기치 못한 오류를 일관된 예외로 감싼다.
            throw new TursoException($"Turso 통신 중 오류가 발생했습니다: {ex.Message}", ex);
        }
    }

    // CLR 값을 Hrana 인자 객체로 변환한다.
    private static object BuildArg(object? value) => value switch
    {
        null => new { type = "null" },
        long l => new { type = "integer", value = l.ToString() },
        int i => new { type = "integer", value = i.ToString() },
        bool b => new { type = "integer", value = b ? "1" : "0" },
        double d => new { type = "float", value = d },
        _ => new { type = "text", value = value.ToString() ?? "" }
    };

    // pipeline 응답 JSON 을 파싱하여 컬럼명과 행 데이터로 변환한다.
    private static TursoResult ParseResult(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var results = root.GetProperty("results");
        if (results.GetArrayLength() == 0)
            return new TursoResult();

        var first = results[0];

        // 개별 statement 단위 오류 처리
        if (first.GetProperty("type").GetString() == "error")
        {
            var msg = first.GetProperty("error").GetProperty("message").GetString();
            throw new TursoException($"SQL 실행 오류: {msg}");
        }

        var execResult = first.GetProperty("response").GetProperty("result");

        var result = new TursoResult();

        // last_insert_rowid / affected_row_count 추출
        if (execResult.TryGetProperty("last_insert_rowid", out var rowid)
            && rowid.ValueKind == JsonValueKind.String
            && long.TryParse(rowid.GetString(), out var id))
        {
            result.LastInsertRowId = id;
        }
        if (execResult.TryGetProperty("affected_row_count", out var affected))
        {
            result.AffectedRows = affected.GetInt32();
        }

        // 컬럼명 목록
        var cols = execResult.GetProperty("cols");
        foreach (var col in cols.EnumerateArray())
            result.Columns.Add(col.GetProperty("name").GetString() ?? "");

        // 각 행을 컬럼명 -> 값 딕셔너리로 변환
        foreach (var row in execResult.GetProperty("rows").EnumerateArray())
        {
            var rowDict = new Dictionary<string, object?>();
            for (var c = 0; c < result.Columns.Count; c++)
            {
                rowDict[result.Columns[c]] = ReadCell(row[c]);
            }
            result.Rows.Add(rowDict);
        }

        return result;
    }

    // Hrana 셀 값({type,value})을 CLR 값으로 변환한다.
    private static object? ReadCell(JsonElement cell)
    {
        var type = cell.GetProperty("type").GetString();
        return type switch
        {
            "null" => null,
            "integer" => long.TryParse(cell.GetProperty("value").GetString(), out var l) ? l : 0L,
            "float" => cell.GetProperty("value").GetDouble(),
            _ => cell.GetProperty("value").GetString()
        };
    }
}

// Turso 통신 전용 예외
public class TursoException : Exception
{
    public TursoException(string message) : base(message) { }
    public TursoException(string message, Exception inner) : base(message, inner) { }
}

// SQL 실행 결과 컨테이너
public class TursoResult
{
    public List<string> Columns { get; } = new();                 // 컬럼명 목록
    public List<Dictionary<string, object?>> Rows { get; } = new(); // 행 데이터
    public long LastInsertRowId { get; set; }                     // INSERT 후 생성된 PK
    public int AffectedRows { get; set; }                         // 영향받은 행 수
}
