# Lawgivers II Control 1.3.1 라이브 테스트

## 화면 없는 런타임 자가진단

1. 게임을 종료합니다.
2. `UserData\LawgiversControl\runtime-self-test.flag`라는 빈 파일을 만듭니다.
3. 게임을 실행해 메인 메뉴가 로드될 때까지 기다린 뒤 종료합니다.
4. `UserData\LawgiversControl\runtime-self-test.json`에서 `Passed`가 `true`인지 확인합니다.

이 검사는 실제 IL2CPP 게임 타입과 네이티브 접근자를 사용하지만, 임시 객체만 사용하므로 저장 게임을 변경하지 않습니다.

기존 저장을 보호하려면 반드시 새 싱글플레이 테스트 게임을 사용하십시오.

1. Lawgivers II를 실행하고 새 싱글플레이 게임을 만듭니다.
2. 게임 화면이 완전히 열린 뒤 다음 파일이 생성됐는지 확인합니다.
   - `UserData\LawgiversControl\catalog.json`
   - `UserData\LawgiversControl\last-apply.json`
3. `catalog.json`에서 테스트할 인물, 정당, 국가의 ID를 확인합니다.
4. 게임을 종료하고 `config.json`의 예제 이름을 실제 ID로 바꿉니다.
5. 행동력은 `ActionPoints.Value`와 `ActionPoints.Max`에 테스트 값을 입력합니다.
6. 확률 테스트가 필요하면 `ForceProbabilitySuccess`를 `true`로 바꿉니다.
7. 게임을 다시 실행해 동일한 테스트 저장을 불러옵니다.
8. 게임 주차 표시가 나타난 뒤 다음 명령을 실행합니다.

```powershell
powershell -ExecutionPolicy Bypass -File .\verify.ps1 -RequireLiveReport
```

9. `last-apply.json`에서 다음 값을 게임 화면과 대조합니다.
   - `People[].Attributes`, `Wealth`, `PartyId`
   - `Parties[].Money`
   - `ActionPoints.Value`, `ActionPoints.Max`
   - `Nations[].Armies`, `ArmyUnits`, `Missiles`

테스트가 끝나면 `ForceProbabilitySuccess`를 다시 `false`로 바꾸십시오. 실제 저장에 적용하기 전에는 `config.json`과 저장 파일을 별도로 백업하는 것이 안전합니다.

## 인게임 UI 테스트

1. 싱글플레이 저장 게임에서 인물 창의 능력치 탭을 엽니다.
2. 기존 능력치 행 아래에 `CHEAT · 모두 최대`가 표시되는지 확인하고 실행합니다.
3. 정당 창에서 `구성원`을 눌러 오른쪽 구성원 목록을 엽니다.
4. 구성원 목록 맨 위의 `CHEAT · 모두 최대`를 실행하고 소속자 전원의 능력치와 충성도를 확인합니다.
5. 별도 오버레이나 대상 선택 목록이 생성되지 않는지 확인합니다.
6. `UserData\LawgiversControl\ui-runtime.json`에서 `Mode`가 `ContextIntegrated`, `SeparateOverlay`와 `CustomInputFields`가 `false`인지 확인합니다.
