# Lawgivers II Control 1.3.1

Lawgivers II(Steam, Windows x86 IL2CPP)용 설정 기반 싱글플레이 모드입니다.

## 기능

- IL2CPP 네이티브 호출에 반응하지 않던 Harmony UI 훅을 활성 UI 폴링으로 교체
- 버튼 동작을 실제 `Button.onClick` 네이티브 UnityEvent에 연결
- `EDIT_*` 예제 설정을 실행 규칙에서 자동 제외하여 반복 경고와 trampoline 오류 방지

- 별도 오버레이와 중복 대상 선택 UI 제거
- 현재 인물의 기존 능력치 창 아래에 `CHEAT · 모두 최대` 직접 통합
- 현재 정당의 기존 오른쪽 구성원 목록 맨 위에 `CHEAT · 모두 최대` 직접 통합
- 작동하지 않던 자체 TMP 입력창 제거; 임의 자금·행동력 변경은 `config.json`으로 유지
- 게임의 실제 TextMeshPro 한글 폰트와 목록 레이아웃 재사용
- 특정 인물의 모든 능력치·충성도 최대화
- 특정 정당 소속 모든 인물의 모든 능력치·충성도 최대화
- 개인·정당·국가 자금 입력값 추가
- 플레이어 행동력 입력값 추가
- 동일 SHA-256의 기존 MelonLoader 0.7.3 x86 공유 설치 지원
- 특정 인물 또는 특정 정당 소속 인물의 능력치, 충성도, 재산, 정당 변경
- 정당 자금과 플레이어 행동력 변경
- 확률 판정 100% 성공 옵션
- 국가 군대 수, 부대별 병력, 미사일 수 설정 옵션
- 현재 월드의 인물·정당·국가 ID 카탈로그 생성
- 실제 IL2CPP 객체를 사용하는 비파괴 런타임 자가진단

## 설치

아래 원라인 명령을 PowerShell에서 실행합니다.

```powershell
$p="$env:TEMP\LawgiversControl-install.ps1"; iwr 'https://raw.githubusercontent.com/bbggkkk/lawgivers2mode/main/install-online.ps1' -OutFile $p; powershell.exe -NoProfile -ExecutionPolicy Bypass -File $p
```

설치기는 GitHub Release의 `SHA256SUMS.txt`와 다운로드 ZIP을 검증하고 필요한 경우 관리자 권한을 요청합니다. 기존 `config.json`은 덮어쓰지 않습니다.

게임 업데이트나 다른 모드 로더와 충돌할 수 있으므로 저장 파일을 백업하고 싱글플레이에서만 사용하십시오.
