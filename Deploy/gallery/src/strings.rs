//! Every word the gallery shows a student or the teacher, in Korean. This is the one home for UI text: pages,
//! buttons, messages and the slips. The scripts in `static/` get their messages from `data-` attributes rendered
//! from here. Unity menu names stay as Unity shows them. (The operator's command line in `cli.rs` stays English.)

pub const SITE_NAME: &str = "Daedalus 갤러리";
pub const LOGO_EN: &str = "DAEDALUS";
pub const LOGO_KO: &str = "갤러리";
pub const NAV_GAMES: &str = "게임 목록";
pub const NAV_UPLOAD: &str = "업로드";
pub const NAV_TEACHER: &str = "선생님";
pub const SIGN_OUT: &str = "로그아웃";
pub const NAV_PASSWORD: &str = "비밀번호 변경";
pub const NAV_LOGIN: &str = "로그인";
pub const SHARED_LAPTOP_NOTE: &str = "같이 쓰는 컴퓨터라면 끝날 때 꼭 로그아웃하세요.";

pub fn signed_in_as(name: &str) -> String {
    format!("{name} 님으로 로그인됨")
}

// Change password
pub const PASSWORD_TITLE: &str = "비밀번호 변경";
pub const PASSWORD_CURRENT: &str = "지금 비밀번호";
pub const PASSWORD_NEW: &str = "새 비밀번호";
pub const PASSWORD_CONFIRM: &str = "새 비밀번호 확인";
pub const PASSWORD_BUTTON: &str = "바꾸기";
pub const PASSWORD_RULE: &str = "8글자 이상. 아이디나 지금 비밀번호와 달라야 해요.";
pub const PASSWORD_WRONG_CURRENT: &str = "지금 비밀번호가 틀렸어요.";
pub const PASSWORD_MISMATCH: &str = "새 비밀번호 두 칸이 서로 달라요.";
pub const PASSWORD_TOO_SHORT: &str = "새 비밀번호는 8글자 이상이어야 해요.";
pub const PASSWORD_TOO_LONG: &str = "새 비밀번호는 128글자까지예요.";
pub const PASSWORD_SAME_AS_LOGIN: &str = "새 비밀번호가 아이디와 같아요.";
pub const PASSWORD_SAME_AS_CURRENT: &str = "새 비밀번호가 지금 비밀번호와 같아요.";
pub const PASSWORD_CHANGED: &str = "비밀번호를 바꿨어요. 다른 브라우저에서는 로그아웃됐어요.";

// Sign in
pub const LOGIN_TITLE: &str = "로그인";
pub const LOGIN_ID: &str = "아이디";
pub const LOGIN_PASSWORD: &str = "비밀번호";
pub const LOGIN_BUTTON: &str = "로그인";
pub const LOGIN_WRONG: &str = "아이디나 비밀번호가 틀려요.";
pub const LOGIN_BUSY: &str = "잠시 후 다시 해 보세요.";
pub const LOGIN_WELCOME: &str = "게임을 올리려면 로그인하세요";
pub const LOGIN_HINT: &str = "아이디와 비밀번호는 선생님께 받아요.";

pub fn wait_seconds(seconds: u64) -> String {
    format!("{seconds}초 뒤에 다시 해 보세요.")
}

// Game list
pub const MY_GAMES: &str = "내 게임";
pub const MY_GAMES_EMPTY: &str = "아직 올린 게임이 없어요.";
pub const UPLOAD_FIRST: &str = "게임 올리기";
pub const ALL_GAMES: &str = "모든 게임";
pub const NO_GAMES: &str = "아직 게임이 없어요.";
pub const NEW_BADGE: &str = "NEW";

pub fn game_count(n: usize) -> String {
    format!("게임 {n}개")
}

/// When a game was last uploaded, in whole Korean calendar days before today.
pub fn uploaded_ago(days: i64) -> String {
    match days {
        ..=0 => "오늘 올림".to_string(),
        1 => "어제 올림".to_string(),
        n => format!("{n}일 전에 올림"),
    }
}
pub const REMOVE: &str = "삭제";
pub const REMOVE_BUTTON: &str = "삭제하기";
pub const CANCEL: &str = "취소";

pub fn remove_game_confirm(title: &str) -> String {
    format!("'{title}' 게임을 삭제할까요? 되돌릴 수 없어요.")
}

// Play view (gallery) and player page (games host)
pub const BACK_TO_GAMES: &str = "← 게임 목록";
pub const FULLSCREEN: &str = "전체 화면";
pub const NEEDS_KEYBOARD: &str = "키보드나 게임패드가 필요해요.";
pub const COPY_LINK: &str = "링크 복사";
pub const LINK_COPIED: &str = "복사했어요!";
pub const LINK_SELECTED: &str = "링크를 선택했어요. 복사해서 보내 주세요.";
pub const SHARE_LINK_LABEL: &str = "이 게임의 링크";

pub fn made_by(name: &str) -> String {
    format!("{name} 만듦")
}
pub const REOPEN_FROM_GALLERY: &str = "갤러리에서 게임을 다시 열어 주세요.";
pub const PLAYER_LOAD_FAILED: &str = "게임을 불러오지 못했어요. 갤러리에서 다시 열어 주세요.";

// Upload page
pub const UPLOAD_TITLE: &str = "게임 올리기";
pub const UPDATE_MY_GAME: &str = "내 게임 업데이트하기";
pub const NEW_GAME: &str = "새 게임";
pub const CHOOSE_FILE: &str = "Daedalus → Share my game 으로 만든 zip 파일";
pub const UPLOAD_BUTTON: &str = "올리기";
pub const STEP_MAKE: &str = "Unity에서 zip 만들기";
pub const STEP_MAKE_HOW: &str = "메뉴에서 Daedalus → Share my game 을 누르면 zip 파일이 생겨요.";
pub const STEP_PICK: &str = "새 게임이에요, 아니면 업데이트예요?";
pub const STEP_FILE: &str = "zip 파일 고르기";
pub const DROP_HINT: &str = "누르거나 여기로 끌어 오세요";
pub const UPLOADING: &str = "올리는 중…";
pub const PICK_ZIP: &str = "zip 파일을 골라 주세요.";
pub const PICK_GAME: &str = "업데이트할 게임을 골라 주세요.";
pub const UPLOAD_NETWORK_ERROR: &str = "올리다가 연결이 끊겼어요. 다시 해 보세요.";
pub const LOGIN_AGAIN: &str = "다시 로그인하기";

pub fn file_too_big(max_bytes: u64) -> String {
    format!("파일이 너무 커요. {} MB까지 올릴 수 있어요.", max_bytes / 1_000_000)
}

// Upload answers
pub const SESSION_ENDED: &str = "로그인이 끝났어요. 다시 로그인한 뒤 올려 주세요.";
pub const UPLOAD_BUSY: &str = "업로드 중이에요. 끝난 뒤 다시 해 보세요.";
pub const DISK_FULL: &str = "갤러리 저장 공간이 꽉 찼어요. 선생님께 알려 주세요.";
pub const GAME_NOT_FOUND: &str = "그 게임을 찾을 수 없어요.";
pub const UPLOAD_FAILED: &str = "올리지 못했어요. 다시 해 보세요.";
pub const SERVER_BUSY: &str = "갤러리가 바빠요. 잠시 후 다시 해 보세요.";

pub fn too_many_games(max: i64) -> String {
    format!("게임은 {max}개까지 올릴 수 있어요. 안 쓰는 게임을 지운 뒤 다시 해 보세요.")
}

// Share zip rejections: each says what to do next.
pub const REJECT_NOT_ZIP: &str = "zip 파일이 아니에요. Daedalus → Share my game 으로 만든 zip을 올려 주세요.";
pub const REJECT_UNSAFE: &str = "이 zip은 열 수 없어요. Daedalus → Share my game 으로 다시 만들어 주세요.";
pub const REJECT_PROJECT: &str =
    "Unity 프로젝트 전체를 압축했어요. Unity에서 Daedalus → Share my game 을 누르고, 그 zip을 올려 주세요.";
pub const REJECT_SCREENSHOTS: &str =
    "스크린샷 폴더를 압축한 것 같아요. Daedalus → Share my game 으로 만든 zip을 올려 주세요.";
pub const REJECT_WRAPPED: &str = "zip 안에 폴더가 한 겹 더 있어요. Share my game 이 만든 zip을 그대로 올려 주세요.";
pub const REJECT_NOT_DAEDALUS: &str =
    "Daedalus가 만든 zip이 아니에요. Unity에서 Daedalus → Share my game 을 눌러 만들어 주세요.";
pub const REJECT_BUILD_SETTINGS: &str = "Build Settings 로 만든 빌드예요. Daedalus → Share my game 을 써 주세요.";
pub const REJECT_FORMAT: &str = "Daedalus를 업데이트한 뒤 다시 Share my game 으로 만들어 주세요.";
pub const REJECT_RESHARE: &str =
    "zip이 망가졌거나 빠진 파일이 있어요. Daedalus → Share my game 으로 다시 만들어 주세요.";
pub const REJECT_TOO_BIG: &str = "게임이 너무 커요. 큰 그림이나 소리를 줄인 뒤 Share my game 으로 다시 만들어 주세요.";

// Teacher page
pub const TEACHER_TITLE: &str = "선생님 페이지";
pub const STUDENTS: &str = "학생";
pub const NO_STUDENTS: &str = "아직 학생이 없어요.";
pub const COL_ID: &str = "아이디";
pub const COL_NAME: &str = "이름";
pub const COL_NICKNAME: &str = "별명";
pub const NAMES_HINT: &str = "별명은 갤러리에서 누구나 볼 수 있어요. 이름은 선생님만 봐요.";
pub const RENAME: &str = "바꾸기";
pub const COL_GAMES: &str = "게임 수";
pub const COL_STATUS: &str = "상태";
pub const RESET_PASSWORD: &str = "비밀번호 재설정";
pub const REMOVE_ACCOUNT: &str = "계정 삭제";
pub const ADD_ONE: &str = "학생 한 명 추가";
pub const PASTE_MANY: &str = "여러 명 붙여넣기";
pub const PASTE_HINT: &str = "한 줄에 한 명: 아이디, 이름, 별명";
pub const PREVIEW: &str = "미리 보기";
pub const CREATE: &str = "만들기";
pub const LINE_OK: &str = "좋아요";
pub const LINE_BAD_ID: &str = "아이디가 올바르지 않아요 (영문 소문자·숫자·-·_ 3–20자)";
pub const LINE_NO_NAME: &str = "이름이 없어요";
pub const LINE_NO_NICKNAME: &str = "별명이 없어요";
pub const LINE_DUPLICATE: &str = "붙여넣은 목록에 같은 아이디가 있어요";
pub const LINE_TAKEN: &str = "이미 있는 아이디예요";
pub const NOTHING_TO_ADD: &str = "추가할 학생이 없어요.";
pub const FIX_ERRORS: &str = "오류가 있는 줄을 고친 뒤 다시 미리 보기를 눌러 주세요.";
pub const SLIPS_TITLE: &str = "계정 쪽지";
pub const SLIPS_NOTE: &str = "이 비밀번호는 지금만 볼 수 있어요. 인쇄하거나 적어 두세요.";
pub const PRINT: &str = "인쇄";
pub const SLIP_ADDRESS: &str = "주소";
pub const SLIP_ID: &str = "아이디";
pub const SLIP_PASSWORD: &str = "비밀번호";
pub const NEW_PASSWORD_TITLE: &str = "새 비밀번호";
pub const BACK_TO_TEACHER: &str = "← 선생님 페이지";

pub fn remove_account_confirm(real_name: &str, nickname: &str, games: i64) -> String {
    format!("'{real_name}' ({nickname}) 계정과 게임 {games}개를 삭제할까요? 되돌릴 수 없어요.")
}

// Errors
pub const FORBIDDEN: &str = "이 페이지는 볼 수 없어요.";
pub const NOT_FOUND: &str = "페이지를 찾을 수 없어요.";
