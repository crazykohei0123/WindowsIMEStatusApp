# AGENTS.md

## Project

WindowsIMEStatusApp (ImeStatusOverlay) — Windows 11 常駐アプリ。IME の ON(あ)/OFF(A) 切替をタスクバーインジケータの画素判定で検知し、画面中央に「IME ON」「IME OFF」を一瞬表示する。IMM32/TSF・フック・DLL注入は不使用。

## Setup

- Windows アプリは WSL から Windows 側のツールを直接呼んでビルド・実行する（Wine 不要）。
- .NET 9 SDK は Windows 側にインストール済み（`/mnt/c/Program Files/dotnet/dotnet.exe`）。WSL 側には無い。
- `~/.bashrc` に `dotnet` 関数を定義済み（`dotnet.exe` を呼ぶ）。新しいシェルで `dotnet build` 等がそのまま使える。
- push 認証は Windows 側の Git Credential Manager を経由（`credential.helper` に GCM を設定済み）。
- `net9.0-windows`・WPF・WinForms のため、Windows でしかビルド・実行できない。

## Commands

```bash
# ビルド（Release）
dotnet build src/ImeStatusOverlay/ImeStatusOverlay.csproj -c Release
# 成果物: src/ImeStatusOverlay/bin/Release/net9.0-windows/ImeStatusOverlay.exe

# アイコン再生成（assets/app.svg → assets/app.ico → src 側へ反映）
dotnet run --project tools/IconGen -- assets/app.ico
cp assets/app.ico src/ImeStatusOverlay/app.ico

# デバッグ用 PowerShell（scripts/、Windows で実行）
#   probe-indicator.ps1 / show-glyph.ps1 / watch-indicator.ps1 / inspect-templates.ps1

# テスト実行（xUnit）
dotnet test tests/ImeStatusOverlay.Tests/ImeStatusOverlay.Tests.csproj
```

注意: README のコマンド例はバックスラッシュ（Windows パス）。WSL ではフォワードスラッシュに読み替えること。

## Architecture

- `src/ImeStatusOverlay/` — メインアプリ（WinExe, WPF + WinForms, x64）。
  - `App.xaml(.cs)` エントリ、`Indicator.cs` が UI Automation でタスクバーの IME インジケータ位置を取得、`Classifier.cs` が 200ms ごとにキャプチャしてテンプレートマッチング（あ/A）。
  - `OverlayWindow` が表示、`CalibrationWindow` が初回学習、`SettingsWindow` / `AppSettings` が設定、`StartupRegistration` がスタートアップ登録。
  - 学習データは `%APPDATA%\ImeStatusOverlay\templates.json`。
- `tools/IconGen/` — アイコン生成コンソール（net9.0-windows, WinForms）。`assets/app.svg` をマルチ解像度 ICO に変換。
- `scripts/` — 画素判定デバッグ用の PowerShell。

## Conventions

- テストは xUnit。`tests/ImeStatusOverlay.Tests/` に配置（`Glyph`/`Classifier`/`AppSettings` のロジックをカバー）。`Indicator` は UI Automation/画面キャプチャ依存でテスト対象外。
- `Classifier`/`AppSettings` は `%APPDATA%` へのファイル入出力を `internal` コンストラクタでストアパス注入可能（`InternalsVisibleTo` でテスト公開）。
- `app.manifest` で PerMonitorV2 DPI 設定を意図的に指定している（UIA の rect とキャプチャを同一物理ピクセルに揃えるため）。これにより .NET 9 で `WFO0003` 警告が出るが、ビルドは成功し、この設定は維持する（警告は無視）。
- `.gitignore` で `bin/`・`obj/`・`*.exe` 等を除外済み。ビルド成果物はコミットしない。
- ブランチモデル: `main` がデフォルト。機能開発は `feature/*` ブランチ（既存例: `feature/settings-and-app-icon`）。
