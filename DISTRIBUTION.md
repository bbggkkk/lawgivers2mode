# GitHub 배포 절차

저장소: `bbggkkk/lawgivers2mode`

## 최초 게시

1. 이 저장소의 공개 브랜치에 소스, 문서, `install-online.ps1`을 푸시합니다.
2. GitHub CLI를 설치하고 `gh auth login`으로 인증합니다.
3. 다음 명령으로 Release와 체크섬을 게시합니다.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\publish-release.ps1 -Version 1.3.0
```

게시 스크립트는 빌드, 배포 입력 검사, ZIP 생성, SHA-256 생성, GitHub 업로드를 순서대로 비대화형 실행합니다. `vendor` 입력이나 빌드 산출물이 없으면 Release를 만들지 않고 오류로 종료합니다.

게시 스크립트는 다음 두 자산을 `v1.3.0` Release에 올립니다.

- `Lawgivers-II-Control-1.3.0.zip`
- `SHA256SUMS.txt`

## 사용자 설치 명령

```powershell
$p="$env:TEMP\LawgiversControl-install.ps1"; iwr 'https://raw.githubusercontent.com/bbggkkk/lawgivers2mode/main/install-online.ps1' -OutFile $p; powershell.exe -NoProfile -ExecutionPolicy Bypass -File $p
```

특정 버전을 설치하려면 마지막 호출에 `-Version v1.3.0`을 추가합니다.

## 새 버전 배포

1. 소스와 문서의 버전을 함께 올립니다.
2. 빌드·설치·런타임 자가진단을 통과시킵니다.
3. 변경 사항을 원격 저장소에 푸시합니다.
4. `publish-release.ps1 -Version X.Y.Z`를 실행하여 ZIP 생성부터 게시까지 완료합니다.

릴리즈 ZIP은 약 50MB이므로 Git 저장소에 커밋하지 않고 Release 자산으로만 보관합니다.
