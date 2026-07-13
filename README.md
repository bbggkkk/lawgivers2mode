# Lawgivers II Control 1.2.0

## 원라인 설치

```powershell
$p="$env:TEMP\LawgiversControl-install.ps1"; iwr 'https://raw.githubusercontent.com/bbggkkk/lawgivers2mode/main/install-online.ps1' -OutFile $p; powershell.exe -NoProfile -ExecutionPolicy Bypass -File $p
```

설치기는 최신 [GitHub Release](https://github.com/bbggkkk/lawgivers2mode/releases/latest)를 내려받아 SHA-256을 검증하고 설치합니다. Steam App ID `1407180`을 사용하여 모든 Steam 라이브러리에서 게임 위치를 자동 탐색하며, 찾지 못하면 파일 선택 창 없이 오류를 출력합니다. 기존 `config.json`은 보존됩니다.

Lawgivers II의 인물·정당·플레이어·군사 데이터를 JSON 설정으로 변경하는 싱글플레이용 모드입니다.

분석 및 호환 대상은 Steam build 7332, Unity `6000.3.19f1`, 32비트 IL2CPP, metadata v39입니다. 이 조합은 일반 MelonLoader 설치만으로 충돌하므로 설치 스크립트가 다음 호환 처리를 함께 적용합니다.

- Cpp2IL/Il2CppInterop의 x86 XRef 스캔 비활성화
- x86 .NET 7.0.20 사용
- 이 게임에서 충돌하는 MelonLoader 지원 클래스 주입 비활성화

원본 DLL은 패치 위치에 `.bak` 파일로 보존됩니다.

## 기능

- 화면 우측 상단 `Control` 버튼으로 인물·정당·국가·행동력을 즉시 제어
- 특정 인물의 모든 능력치와 충성도를 실제 최대값으로 설정
- 특정 정당 소속 모든 인물의 모든 능력치와 충성도를 한 번에 최대화
- 개인·정당·국가 자금에 입력한 금액 추가
- 플레이어 행동력에 입력한 값 추가
- ID 또는 이름으로 특정 인물 선택
- 특정 정당 소속 인물 전체 선택
- 인물 능력치, 충성도, 재산, 소속 정당 변경
- 정당 자금 변경
- 플레이어 행동력 현재값·최대값 변경
- 확률 판정을 항상 성공시키는 선택 기능
- 국가별 군단 수, 군단별 병력, 미사일 수 설정

싱글플레이에서만 사용하십시오. 멀티플레이에서 상태를 바꾸면 동기화 오류가 생길 수 있습니다.

## 인게임 UI

싱글플레이 저장 게임을 불러온 뒤 화면 우측 상단의 `Control` 버튼을 누르면 메뉴가 열립니다. 이 버튼은 MelonLoader의 프레임 콜백에 의존하지 않는 Unity Canvas UI입니다.

- `인물`: 페이지에서 인물을 선택하고 모든 능력치·충성도 최대화 또는 개인 자금 추가
- `정당 전체`: 정당을 선택하고 현재 소속된 모든 인물 최대화 또는 정당 자금 추가
- `국가`: 국가를 선택하고 국가 예산에 입력 금액 추가
- `행동력`: 현재 행동력에 입력값 추가

버튼을 누르는 즉시 현재 월드에 적용되며 결과는 메뉴 상태 줄과 `last-apply.json`에 기록됩니다. 입력값은 정수이며 음수를 입력하면 그만큼 차감됩니다.

## 설치

게임을 종료한 뒤 이 폴더에서 PowerShell로 실행합니다. Steam 설치 위치는 자동으로 찾으며 쓰기 권한이 필요한 경우 관리자 권한을 요청합니다. 동일한 공식 MelonLoader 0.7.3 x86이 이미 있으면 안전하게 공유하고 제거 시 기존 로더를 보존합니다.

```powershell
powershell -ExecutionPolicy Bypass -File .\install.ps1
```

Steam 외 위치이거나 자동 탐색이 실패하면 경로를 명시할 수 있습니다.

```powershell
powershell -ExecutionPolicy Bypass -File .\install.ps1 -GamePath 'D:\Games\Lawgivers II'
```

게임을 실행하면 다음 파일을 사용합니다.

- `<게임 경로>\UserData\LawgiversControl\config.json`: 변경 규칙
- `<게임 경로>\UserData\LawgiversControl\catalog.json`: 현재 게임의 인물·정당·국가 이름과 ID
- `<게임 경로>\UserData\LawgiversControl\last-apply.json`: 적용 직후 다시 읽은 실제 인물·정당·행동력·군사 값
- `<게임 경로>\UserData\LawgiversControl\runtime-self-test.json`: 실제 IL2CPP 객체를 사용한 비파괴 런타임 자가진단 결과
- `<게임 경로>\MelonLoader\Latest.log`: 모드 실행 로그

게임 장면이 로드되거나 저장 게임 로드가 감지된 뒤 게임 주차가 처음 갱신될 때 최신 `config.json`을 읽어 적용합니다. 실행 중 설정을 바꾼 경우 저장 게임이나 메인 메뉴를 다시 불러오십시오. 호환성 때문에 이 빌드에서는 1초 폴링이 보장되지 않습니다.

화면 조작 없이 런타임 계약을 검사하려면 `UserData\LawgiversControl\runtime-self-test.flag` 빈 파일을 만든 뒤 게임을 실행하십시오. 모드는 저장 게임과 전역 월드를 변경하지 않는 임시 실제 게임 객체로 인물 선택·능력치·충성도·재산·정당·정당 자금·행동력·군부대·미사일·확률 기능을 검사하고 `runtime-self-test.json`을 기록한 뒤 플래그를 삭제합니다.

## 설정

전체 형식은 [config.example.json](./config.example.json)을 참고하십시오. 설치되는 기본값은 행동력을 변경하지 않도록 `null`입니다.

능력치 키는 다음과 같습니다.

- `recognition`
- `energy`
- `experience`
- `popularity`
- `charm`
- `eloquence`
- `cunning`
- `influence`

`People`은 개인 규칙, `Parties[].Members`는 해당 정당 소속 인물 전체 규칙입니다. `Changes.Party` 또는 `Changes.PartyId`로 새 소속 정당을 지정합니다. 번역된 이름이 바뀔 가능성이 있으므로 `catalog.json`의 ID 사용이 가장 안전합니다.

`ForceProbabilitySuccess`를 `true`로 설정하면 게임의 `Somnium.Math.Random.Probability` 판정을 성공으로 바꿉니다. AI와 세계 이벤트에도 영향을 줄 수 있습니다.

`Nations[].Armies`는 군단 수, `ArmyUnits`는 각 군단의 병력, `Missiles`는 미사일 표시·판정값을 설정합니다.

## 검증

배포 빌드, 회귀 테스트, 설정 JSON, 설치 DLL 해시를 한 번에 확인:

```powershell
powershell -ExecutionPolicy Bypass -File .\verify.ps1
```

싱글플레이 저장 게임을 불러온 뒤 실제 라이브 적용 보고서까지 확인:

```powershell
powershell -ExecutionPolicy Bypass -File .\verify.ps1 -RequireLiveReport
```

전체 라이브 테스트 순서는 [TESTING.md](./TESTING.md)를 참고하십시오.

## 제거

모드만 제거:

```powershell
powershell -ExecutionPolicy Bypass -File .\uninstall.ps1
```

이 모드가 설치한 MelonLoader까지 제거:

```powershell
powershell -ExecutionPolicy Bypass -File .\uninstall.ps1 -RemoveLoader
```

설정도 함께 지우려면 `-RemoveConfig`를 추가하십시오.
