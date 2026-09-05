# WindowsIMEStatusApp 残作業(WSL 引き継ぎ用)

対象: PR #4「fix: 未知字形(×・空欄等)をUnknownとして拒否し誤表示を防止」のフォローアップ
PR: https://github.com/crazykohei0123/WindowsIMEStatusApp/pull/4
ブランチ: `feature/reject-unknown-glyphs`
元計画: `IME_STATUS_APP_UNKNOWN_CHANGE_PLAN.md`(§番号はこの計画を指す)

## 実装済み(背景)

- `Classifier.Classify()`: 入力検査厳格化 + 絶対受理しきい値(暫定 `DefaultAcceptanceThreshold = 20.0`)+ 受理→分離マージン→最近傍の判定順序
- `OverlayWindow.ShowState()`: `Unknown` を冒頭で拒否
- `App.Poll()` / `ImeStateTracker`: 変更なし(要件どおりの既存動作、テストで明文化)
- テスト: 純粋ロジック 45 件すべて合格、Release ビルド成功(WFO0003 警告のみ)

## 1. しきい値の実測と調整(最優先・計画 §4.1-A, §7-8)

`20.0` は暫定値。グレースケール平均絶対差(0-255/px)の実測で決定する。

手順(WSL/Windows で実施):

1. `scripts/watch-indicator.ps1` で距離を採取する
   - 通常表示の `A` vs `A` テンプレートの距離(複数回)
   - 通常表示の `あ` vs `あ` テンプレートの距離(複数回)
   - アンチエイリアス差のある同一字形の距離
   - 「×」・空欄・別アイコンと両テンプレートの距離
2. 正常群の最大距離に余裕を持ち、未知群の最小距離を下回る値を
   `src/ImeStatusOverlay/Recognition/Classifier.cs` の `DefaultAcceptanceThreshold` に設定
3. 採用値と根拠(実測値)をコミットメッセージまたは PR に記録(計画 §7-8 の要件)

調整時の注意:

- 厳しすぎると正しい `A`/`あ` まで Unknown 化する → 実機で A↔あ 切替を必ず確認
- テストは `internal Classifier(storePath, acceptanceThreshold, separationMargin)` で注入値を使うため、本番定数を変えても既存テストは壊れない

実測で正常群と未知群が分離できない場合(計画 §9 の次段候補):

- `Glyph.Signature()` による二値形状比較との併用
- 背景除外マスク
- テンプレートごとの複数サンプル保持

## 2. Windows 11 実機テスト(計画 §5.4)

- [ ] `A` → `あ`: `IME ON` を1回だけ表示
- [ ] `あ` → `A`: `IME OFF` を1回だけ表示
- [ ] トレイ表示が「×」: 何も表示しない
- [ ] 「×」→ `A`: 直前の確定状態がAなら再表示しない
- [ ] 「×」→ `あ`: 確定状態が実際に OFF→ON へ変わった場合のみ表示
- [ ] 全画面アプリの開始・終了・フォーカス移動: ON/OFF の交互表示なし
- [ ] タスクバー自動非表示の表示/非表示: 誤表示なし
- [ ] ライト/ダークテーマ・表示倍率変更後: 必要に応じ再キャリブレーションで正常判定
- [ ] プライマリ/セカンダリタスクバー構成: 既存動作を維持
- [ ] 「今の状態を表示」: 確定状態が `Unknown` なら何も表示しない

実行方法(WSL):

```bash
git checkout feature/reject-unknown-glyphs && git pull
dotnet build src/ImeStatusOverlay/ImeStatusOverlay.csproj -c Release
# 成果物: src/ImeStatusOverlay/bin/Release/net9.0-windows/ImeStatusOverlay.exe
```

## 3. 任意: キャリブレーション誤学習対策(計画 §4.4、別コミット可)

キャリブレーション中に「×」等が頻出すると未知字形をテンプレートとして学習し得る。

- 説明文に「タスクバーが `A` と `あ` を表示している状態で実施する」を明記
- 2パターンの最低出現回数を要求
- 2テンプレート同士が近すぎる場合は学習失敗とする
- 将来: 自動ラベル付けではなく `A`/`あ` を個別ステップで採取する方式

## 4. 条件付き: Indicator の位置特定(計画 §4.5)

実機テストで「×」が別ボタンの拾い漏れと判明した場合のみ対応。
`Indicator.cs` の検索条件は安易に変えない。UIA の `Name`/`AutomationId`/子Image領域を
ログ出力して別課題として位置特定精度を改善する。

## 5. 環境メモ(前環境での検証方法)

PR 作成時の開発機は WSL ではなく通常の Ubuntu で `dotnet` が無かったため、
次の方法で検証した(参考):

- .NET 9 SDK を `/tmp/opencode/dotnet` にインストール
- ビルド: `dotnet build -c Release -p:EnableWindowsTargeting=true`(成功、WFO0003 のみ)
- テスト: テストホストが Linux で WindowsDesktop ランタイムを要求して起動不可のため、
  純粋ロジック 6 ファイル(`ImeState`/`ImeStateTracker`/`Classifier`/`Glyph`/`JsonStore`/`AppSettings`)
  と WPF 非依存のテストを `net9.0` に複製して `dotnet test` → 45/45 合格

WSL では通常どおり `dotnet build` / `dotnet test` が使えるため再複製不要。
