// 메모장 프론트엔드 로직 (바닐라 JS)

// DOM 요소 참조
const noteList = document.getElementById("noteList");
const searchInput = document.getElementById("searchInput");
const newBtn = document.getElementById("newBtn");
const emptyState = document.getElementById("emptyState");
const editorPane = document.getElementById("editorPane");
const titleInput = document.getElementById("titleInput");
const tagsInput = document.getElementById("tagsInput");
const contentInput = document.getElementById("contentInput");
const saveStatus = document.getElementById("saveStatus");
const deleteBtn = document.getElementById("deleteBtn");

let notes = [];            // 현재 로드된 메모 목록
let currentId = null;      // 선택된 메모 ID
let saveTimer = null;      // 자동 저장 디바운스 타이머

// API 호출 공통 래퍼 (예외 처리 포함)
async function api(url, options = {}) {
    try {
        const res = await fetch(url, {
            headers: { "Content-Type": "application/json" },
            ...options,
        });
        if (!res.ok && res.status !== 204) {
            throw new Error(`요청 실패 (${res.status})`);
        }
        return res.status === 204 ? null : await res.json();
    } catch (err) {
        alert("오류가 발생했습니다: " + err.message);
        throw err;
    }
}

// 메모 목록 로드 및 렌더링
async function loadNotes(search = "") {
    const query = search ? `?q=${encodeURIComponent(search)}` : "";
    notes = await api(`/api/notes${query}`);
    renderList();
}

// 목록 렌더링
function renderList() {
    noteList.innerHTML = "";
    if (notes.length === 0) {
        noteList.innerHTML = `<li style="padding:18px;color:#8a9099;font-size:14px;">메모가 없습니다.</li>`;
        return;
    }
    notes.forEach((note) => {
        const li = document.createElement("li");
        li.className = "note-item" + (note.id === currentId ? " active" : "");
        const preview = (note.content || "").replace(/\n/g, " ").slice(0, 60);
        const tagsHtml = (note.tags || "")
            .split(",")
            .map((t) => t.trim())
            .filter(Boolean)
            .map((t) => `<span class="tag">${escapeHtml(t)}</span>`)
            .join("");
        li.innerHTML = `
            <h3>${escapeHtml(note.title) || "(제목 없음)"}</h3>
            <p>${escapeHtml(preview) || "내용 없음"}</p>
            <div class="meta">${tagsHtml} ${formatDate(note.updatedAt)}</div>
        `;
        li.addEventListener("click", () => selectNote(note.id));
        noteList.appendChild(li);
    });
}

// 메모 선택 → 에디터에 표시
function selectNote(id) {
    const note = notes.find((n) => n.id === id);
    if (!note) return;
    currentId = id;
    titleInput.value = note.title;
    tagsInput.value = note.tags;
    contentInput.value = note.content;
    emptyState.classList.add("hidden");
    editorPane.classList.remove("hidden");
    saveStatus.textContent = "저장됨";
    renderList();
}

// 새 메모 생성
async function createNote() {
    const created = await api("/api/notes", {
        method: "POST",
        body: JSON.stringify({ title: "", content: "", tags: "" }),
    });
    notes.unshift(created);
    selectNote(created.id);
    titleInput.focus();
}

// 현재 메모 자동 저장 (디바운스)
function scheduleSave() {
    if (currentId === null) return;
    saveStatus.textContent = "저장 중...";
    clearTimeout(saveTimer);
    saveTimer = setTimeout(saveNote, 700);
}

async function saveNote() {
    if (currentId === null) return;
    const payload = {
        title: titleInput.value,
        content: contentInput.value,
        tags: tagsInput.value,
    };
    await api(`/api/notes/${currentId}`, {
        method: "PUT",
        body: JSON.stringify(payload),
    });
    // 로컬 목록도 갱신
    const note = notes.find((n) => n.id === currentId);
    if (note) {
        Object.assign(note, payload, { updatedAt: new Date().toISOString() });
        // 최신 수정 항목을 맨 위로
        notes = notes.filter((n) => n.id !== currentId);
        notes.unshift(note);
    }
    saveStatus.textContent = "저장됨";
    renderList();
}

// 현재 메모 삭제
async function deleteNote() {
    if (currentId === null) return;
    if (!confirm("이 메모를 삭제할까요?")) return;
    await api(`/api/notes/${currentId}`, { method: "DELETE" });
    notes = notes.filter((n) => n.id !== currentId);
    currentId = null;
    editorPane.classList.add("hidden");
    emptyState.classList.remove("hidden");
    renderList();
}

// 날짜를 읽기 좋은 형식으로 변환
function formatDate(iso) {
    if (!iso) return "";
    try {
        const d = new Date(iso);
        return d.toLocaleDateString("ko-KR", { month: "short", day: "numeric" });
    } catch {
        return "";
    }
}

// XSS 방지를 위한 HTML 이스케이프
function escapeHtml(str) {
    return (str || "").replace(/[&<>"']/g, (c) => ({
        "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;",
    }[c]));
}

// 검색 디바운스
let searchTimer = null;
searchInput.addEventListener("input", () => {
    clearTimeout(searchTimer);
    searchTimer = setTimeout(() => loadNotes(searchInput.value), 300);
});

// 이벤트 바인딩
newBtn.addEventListener("click", createNote);
deleteBtn.addEventListener("click", deleteNote);
titleInput.addEventListener("input", scheduleSave);
tagsInput.addEventListener("input", scheduleSave);
contentInput.addEventListener("input", scheduleSave);

// 초기 로드
loadNotes();
