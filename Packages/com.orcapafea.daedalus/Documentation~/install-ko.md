# Daedalus 설치하고 첫 레벨 만들기

Unity 메뉴 이름은 영어 그대로 적었습니다. Unity Editor를 한국어로 바꾸지 말고 영어로 두세요.

## 1. Unity 설치

1. Unity Hub에서 **Unity 6000.0.85f1**을 설치합니다. 목록에 없으면 [Unity 다운로드 보관소](https://unity.com/releases/editor/archive)에서 이 버전을 찾아 Hub로 설치합니다.
2. 설치할 때 **WebGL Build Support**를 꼭 체크합니다. 빠뜨리면 나중에 게임을 공유할 수 없습니다.

## 2. 프로젝트 만들기

Unity Hub에서 **New project**를 누르고 템플릿은 **Universal 2D**를 고릅니다. 다른 템플릿을 고르면 예제 레벨이 제대로 열리지 않습니다.

## 3. 패키지 저장소 추가

**Edit → Project Settings → Package Manager**를 열고 **Scoped Registries**에 아래 세 값을 넣은 뒤 **Save**(또는 **Apply**)를 누릅니다.

| 칸 | 값 |
| --- | --- |
| Name | `OpenUPM` |
| URL | `https://package.openupm.com` |
| Scope(s) | `com.orcapafea` |

## 4. Daedalus와 예제 설치

1. **Window → Package Manager**를 엽니다.
2. 왼쪽에서 **My Registries**를 고르고 **Daedalus Platformer Maker**를 찾아 **Install**을 누릅니다.
3. 같은 화면의 **Samples** 탭에서 **Starter** 옆의 **Import**를 누릅니다.

가져오기가 끝나도 아무것도 열리지 않습니다. **Project** 창에서 `Assets/Samples/Daedalus Platformer Maker/0.2.0/Starter` 폴더를 찾으세요.

## 5. Console 찾아두기

**Window → General → Console**을 열어 둡니다. 레벨에 문제가 있으면 대부분 여기에 경고가 나옵니다.

## 6. 플레이하고 내 레벨로 저장하기

1. `Starter` 씬을 더블클릭해서 열고 위쪽의 **Play** 버튼을 누릅니다. 방향키나 A/D로 움직이고 Space로 점프합니다. 함정에 몇 번 속아 보세요.
2. **Play**를 다시 눌러 끝냅니다. Play 중에 바꾼 것은 끝나면 사라집니다.
3. **File → Save As**로 `Assets` 폴더 바로 아래에 `My Level` 같은 이름으로 저장합니다. `Assets/Samples` 안은 예제를 다시 가져올 때 덮어쓰일 수 있으니 거기에 저장하면 안 됩니다.

`Assets/Samples` 안의 파일과 프리팹은 직접 고치지 마세요. 패키지를 업데이트하고 예제를 다시 가져오면 그 폴더가 통째로 바뀝니다.

이제부터는 이 사본에서 작업합니다. Unity는 자동으로 저장해 주지 않으니 **Ctrl+S**를 자주 누르세요.

## 7. 함정 만들어 보기

**함정 하나 옮겨 놓기**

1. **Window → 2D → Tile Palette**를 열고 `Starter Palette`를 고릅니다. **Active Tilemap**이 `Collision`인지 확인합니다.
2. 지우개 도구(**D**)로 바닥 타일 세 칸을 지웁니다. 타일이 칠해진 곳에서는 함정이 동작하지 않습니다.
3. `Starter/Prefabs` 폴더에서 `Drop Floor`를 지운 자리로 끌어다 놓습니다.
4. Inspector에 **Snap to grid** 버튼이 보이면 누릅니다. 안 보이면 이미 칸에 맞은 것입니다.

`Falling Ceiling`은 아래쪽의 긴 상자에 플레이어가 들어오면 떨어집니다. 옮긴 뒤에는 그 상자가 바닥까지 닿는지 확인하고, 닿지 않으면 Scene 창에서 상자의 손잡이를 끌어 늘리세요.

`Fake Floor Tile`은 Tile Palette에 있는 타일입니다. 바닥처럼 보이지만 밟으면 그대로 떨어집니다.

`Trap Demo` 폴더의 `Trap Demo` 씬은 모든 종류의 함정이 들어 있는 긴 레벨입니다. 끝까지 해 본 다음, 함정을 하나씩 골라 Inspector 설정을 살펴보세요.

**직접 함정 달기**

1. `Your Turn (add a Trap here)` 오브젝트를 고릅니다. 지금은 그림일 뿐이라 밟으면 떨어집니다.
2. Inspector에서 **Add Component → Daedalus → Trap**을 누릅니다.
3. 그림이 잠깐 사라집니다. **Tile** 칸에 `Ground Tile`을 넣으면 돌아옵니다.
4. Play를 눌러 확인합니다.

## 8. 공유하기

1. **플레이어를 레벨 시작 위치에 되돌려 놓습니다.** 테스트하려고 옮겨 둔 위치에서 게임이 시작됩니다.
2. **Ctrl+S**로 저장합니다.
3. **Daedalus → Share my game**을 누르고 안내를 따릅니다. 처음에는 몇 분 걸립니다.
4. 만들어진 zip 파일을 [수원 게임 갤러리](https://suwon.orcarin.com)에 올립니다.

지금은 시험 기간입니다. 시험 기간에 올린 게임은 지워질 수 있습니다.

## 9. 스테이지 여러 개로 만들기 (선택)

씬 하나가 스테이지 하나입니다. 레벨 하나를 공유해 본 다음에 해 보세요. 예제는 Starter의 `Stage Example` 폴더에 있습니다.

1. **Ctrl+S**로 지금 씬을 저장합니다. 저장하지 않은 내용은 복제되지 않습니다.
2. Project 창에서 씬을 고르고 **Ctrl+D**로 복제합니다. 복제한 씬의 이름을 바꾸고(예: `Stage 2`), 더블클릭해 열어서 새로 그립니다.
3. 첫 번째 씬을 다시 열고 **씬에 놓인 Exit**를 고릅니다. Inspector의 **Next stage** 칸에 다음 씬을 Project 창에서 끌어다 놓습니다. Hierarchy 창으로 끌면 씬이 두 개 열리니 주의하세요.
4. 복제한 씬의 Exit에는 원래 씬의 **Next stage**가 그대로 남아 있습니다. 마지막 스테이지의 Exit는 **Next stage**를 비워 두세요. 거기서 게임이 끝납니다.
5. Player를 프리팹으로 만들어 쓰면 모든 스테이지의 움직임이 같아집니다. `Prefabs/Stage Player`를 참고하세요.
6. 공유할 때는 **첫 번째 스테이지를 열고** Share my game을 누릅니다. 창에 스테이지 순서가 나옵니다.
