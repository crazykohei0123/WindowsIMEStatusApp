# Windows IME状態表示アプリ 全体計画

## なぜIMM32ではダメで、どうすればGoogle 日本語入力に対応できるか

- Google 日本語入力は**TSF専用TIP**であり、IMM32のIMEモジュール(.ime)を持ちません。MS-IMEはmsctf.dllのブリッジ経由でIMM32からも状態を読めますが、Google 日本語入力はこのブリッジに乗らないため、Python+IMM32では検出できませんでした。
- 一方TSFにも制約があります。IMEのON/OFF状態(`GUID_COMPARTMENT_KEYBOARD_OPENCLOSE`コンパートメント)は**スレッド/プロセスごと**に管理され、外部プロセスから他アプリのTSF状態を読むAPIは存在しません。
- したがって正攻法は、**TSFテキストサービス(TIP)としてCOM DLLを登録し、各アプリのプロセス内にTSF本体にロードしてもらう**方式です。キー入力には一切介入せず、コンパートメントの変化を「観察」するだけなので、Google 日本語入力の動作を妨げません。

## アーキテクチャ

```
[各アプリのプロセス内に自動ロード]
ImeMonitorTip.dll  (C++ / COM in-procサーバー)
  ├─ ITfTextInputProcessor     … TSFへの入口(Activate/Deactivateのみ)
  ├─ ITfThreadMgrEventSink     … フォーカス変更の検知
  └─ ITfCompartmentEventSink   … IME ON/OFF変化の検知
        │ Named Pipe \\.\pipe\ImeStatusPipe に "ON"/"OFF" を送信
        ▼
[常駐表示アプリ]
ImeStatusOverlay.exe  (C# / WPF / .NET 8)
  ├─ Named Pipeサーバーで状態を受信
  └─ 透明・クリックスルー・最前面の全画面オーバーレイに
     「IME ON」「IME OFF」を画面中央へ大きく表示 → フェードアウトで自動消去
```

## コンポーネント詳細

### 1. ImeMonitorTip.dll(C++/MSVC)
- `ITfTextInputProcessor::Activate`で以下を初期化:
  - `ITfThreadMgr`から`ITfCompartmentMgr`を取得し、`GUID_COMPARTMENT_KEYBOARD_OPENCLOSE`の変化を`ITfSource::AdviseSink`で購読
  - `ITfThreadMgrEventSink`を購読し、フォーカス移動時に状態を再読(アプリ切替時の表示に対応)
  - 初回の状態を読んでパイプへ送信
- `OnChange`でコンパートメント値(0=OFF/非0=ON)を読み、`"ON"`/`"OFF"`をパイプへ書き込む
- **重要な設計制約**: TSFのコールバック内でブロッキング処理は厳禁(全アプリがハングする)。パイプ送信は短タイムアウトorワーカースレッド経由。パイプ未接続(表示アプリ未起動)時は即座にドロップ
- キーイベントシンクは実装しない完全受動型 → 入力処理に影響ゼロ
- 登録はHKCUのみで管理者権限不要:
  - `HKCU\Software\Classes\CLSID\{CLSID}\InprocServer32`
  - `HKCU\SOFTWARE\Microsoft\CTF\TIP\{CLSID}\LanguageProfile\0x00000411\{GUID}`
- 64bit版をまず作成(32bitアプリ対応は必要になれば追加ビルド)

### 2. ImeStatusOverlay(C# WPF / .NET 8)
- オーバーレイウィンドウ: `WindowStyle=None` + `AllowsTransparency=true` + `Topmost` + `ShowActivated=false`
- クリックスルー: Win32相互運用で`WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE`を付与
- 表示: 中央配置の巨大テキスト(目安96pt前後、ON=緑系/OFF=グレー系、縁取り・影付きで可読性確保)
- アニメーション: フェードイン(約100ms)→保持(約800ms)→フェードアウト。連続切替時はアニメーションをリセット
- マルチモニタ: フォアグラウンドウィンドウのあるモニタの中央に表示
- Named Pipeサーバー(非同期ループ)→ UIスレッドへマーシャリング
- タスクトレイ常駐(終了メニュー付き)、多重起動防止(Mutex)
- 設定(任意): 表示時間、フォントサイズ、表示位置(中央/上部)、常時表示モード

### 3. 登録/解除ツール
- `register.ps1`/`unregister.ps1`(またはDLLに`DllRegisterServer`実装+regsvr32): COM登録 + TSFプロファイル登録 + 入力リストへの有効化
- 表示アプリのスタートアップ登録(レジストリRunキー)

## 開発手順(この順で進めます)

1. **環境セットアップ**: C++コンパイラが見つからなかったため、Visual Studio Build Tools(C++ワークロード)をインストール。.NET SDKは`dotnet`が検出済み(バージョンはActモードで再確認)
2. **スパイク(技術検証)**: 最小TIP DLLを作り、コンパートメント変化を`OutputDebugString`に出す → DebugViewでNotepad上のGoogle 日本語入力のON/OFF(半角/全角キー)が捕捉できることを確認。ここが最大の技術的山場
3. **IPC**: パイプ送信を実装し、コンソールの受信側で確認
4. **WPFオーバーレイ**: 表示・フェード・クリックスルー・マルチモニタ対応
5. **登録スクリプト + 自動起動**
6. **テスト**: Win11 Notepad、エクスプローラ(リネーム)、VS Code、Chrome、Windows Terminal / MS-IMEとの併用 / スリープ・ロックからの復帰
7. **仕上げ**: トレイメニュー、設定、README整備

## リスクと注意点

- TIP DLLのクラッシュはロード先の全アプリを巻き込む → 最小限のコード・例外安全・ブロッキング禁止を徹底
- 管理者権限で動くアプリ上ではUIPIの壁により検出できません(必要なら表示アプリの管理者化で対処可能)
- 言語リストにキーボード候補として表示されますが、選択しなければ無害です
- ごく一部の非TSFアプリ(古いIMM専用アプリ)は対象外(フォールバックのIMMポーリング併用も可能ですが今回はスコープ外とします)

## 想定規模

- TIP DLL: C++ 約400〜600行 / オーバーレイ: C#+XAML 約300〜500行 / 登録スクリプト

---

この計画で問題なければ、**Actモードに切り替えて**ください。まず環境セットアップ(Build Toolsの有無の最終確認)と、手順2のスパイク用TIP DLLの骨格作成から着手します。表示の仕様(表示時間・色・位置など)や、常時表示モードの要不要など、調整したい点があればお知らせください。