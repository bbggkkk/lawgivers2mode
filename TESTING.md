# Lawgivers II Control 1.2.1 라이브 테스트

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

1. 싱글플레이 저장 게임을 불러오고 좌측 패널 옆 주황색 `CHEAT` 탭으로 메뉴를 엽니다.
2. `인물`에서 한 명을 선택하고 최대화 버튼과 개인 자금 추가 버튼을 실행합니다.
3. `정당 전체`에서 정당을 선택하고 전체 최대화 버튼을 실행한 뒤 소속자 전원의 능력치와 충성도를 확인합니다.
4. `국가`에서 국가 자금을 추가하고 `행동력`에서 행동력을 추가합니다.
5. 메뉴 상태 줄과 `UserData\LawgiversControl\last-apply.json`의 실제값을 확인합니다.
6. `CHEAT` 탭 또는 닫기 버튼으로 메뉴가 정상적으로 닫히는지 확인합니다.
7. `UserData\LawgiversControl\ui-runtime.json`의 `Created`, `Canvas`, `ToggleButton`, `Panel`, `ButtonCallback`, `FontLoaded`, `FontMaterial`이 모두 `true`인지 확인합니다.
