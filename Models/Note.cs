namespace Notepad.Models;

// 메모(노트) 한 건을 표현하는 모델 클래스
public class Note
{
    public long Id { get; set; }                 // 메모 고유 식별자
    public string Title { get; set; } = "";      // 제목
    public string Content { get; set; } = "";    // 본문 내용
    public string Tags { get; set; } = "";       // 태그 (쉼표로 구분)
    public string CreatedAt { get; set; } = "";  // 생성 시각 (ISO-8601 문자열)
    public string UpdatedAt { get; set; } = "";  // 수정 시각 (ISO-8601 문자열)
}
