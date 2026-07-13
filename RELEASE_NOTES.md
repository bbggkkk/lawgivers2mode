# Lawgivers II Control 1.1.0

Lawgivers II(Steam, Windows x86 IL2CPP)용 설정 기반 싱글플레이 모드입니다.

## 기능

- 특정 인물 또는 특정 정당 소속 인물의 능력치, 충성도, 재산, 정당 변경
- 정당 자금과 플레이어 행동력 변경
- 확률 판정 100% 성공 옵션
- 국가 군대 수, 부대별 병력, 미사일 수 설정 옵션
- 현재 월드의 인물·정당·국가 ID 카탈로그 생성
- 실제 IL2CPP 객체를 사용하는 비파괴 런타임 자가진단

## 설치

아래 원라인 명령을 PowerShell에서 실행합니다.

```powershell
$p="$env:TEMP\LawgiversControl-install.ps1"; iwr 'https://raw.githubusercontent.com/bbggkkk/lawgivers2mode/main/install-online.ps1' -OutFile $p; & $p
```

설치기는 GitHub Release의 `SHA256SUMS.txt`와 다운로드 ZIP을 검증하고 필요한 경우 관리자 권한을 요청합니다. 기존 `config.json`은 덮어쓰지 않습니다.

게임 업데이트나 다른 모드 로더와 충돌할 수 있으므로 저장 파일을 백업하고 싱글플레이에서만 사용하십시오.
