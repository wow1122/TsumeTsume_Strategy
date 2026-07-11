# TsumeTsume_Strategy

ファイアーエムブレムif暗夜をモデルにした 2D タクティクスSRPG。Unity 6 (6000.3.10f1) + URP。
作者はプログラミングほぼ初心者。日本語で、専門用語をかみ砕いて丁寧に説明すること。絵文字は使わない。

## 進捗状況（2026-07-11 時点）

バーティカルスライス完成（Phase 0〜7）、Phase 8（データ基盤刷新）、
Phase 9（前衛・後衛武器と新三すくみ、CombatRules による判定一元化）、
Phase 10（コマンドメニューUI・移動取り消し）、
Phase 11（救出システム）、Phase 12（輸送隊・輸送隊死亡で敗北）まで Play 確認済み・コミット済み。
次は Phase 13（地形）。

全体計画（Phase 8〜15 ロードマップ）は
`/home/wawo/.claude/plans/tsumetsume-strategy-phase8-tsumetsume-st-elegant-parasol.md` 参照（WSL側）。
ゲーム根幹の仕様は `docs/コアシステム仕様書.md`（リポジトリ側を正とする。Desktop の原本は作者の編集用）。

## 確定済みの仕様（作者との合意。勝手に変えない）

- 命中は必中（命中・回避RNGなし）。ダメージ = max(0, 攻撃+武器威力+三すくみ補正-(防御+地形防御))
- 武器分類（WeaponCategory）: 剣・斧・槍＝前衛(Melee=0)、弓・魔法＝後衛(Ranged=1)。データ駆動（WeaponData.category）
- 三すくみ（Phase 9 で新式へ移行済み）: 前衛武器の攻撃のみ。剣>斧>槍>剣。
  有利側が攻撃 = +max(2, 攻撃側の技−防御側の速さ)、有利側が防御 = −max(2, 防御側の速さ−攻撃側の技)。
  後衛武器・武装無しは前衛から攻撃されると「武装無し扱い」で不利判定。後衛側の攻撃時は三すくみなし
- 後衛武器の射程: 移動後は最小射程（2マス）ちょうど、静止時（自マスクリック）は 2〜武器上限（弓3・魔道書2）
- 反撃は基本なし。CombatSystem.CanCounter() フックが唯一の拡張点（現状常に false）
- 挟撃: 近接攻撃時、敵を挟んだ反対側の隣マス(上下左右)に前衛武器の味方がいれば追加攻撃。
  参加した味方は行動を消費しない。遠距離・後衛武器の味方では不成立。
  ガード（無効化）: 防御側の隣接4マスに同陣営の「前衛武器の歩兵」がいると挟撃不成立。
  ガード役自身は守られない。行動済みでも有効。敵味方対称
- 戦闘の可否・補正判定は CombatRules に一元化。UIとAIは必ず同じ関数を呼ぶ。
  合計ダメージ予測は CombatSystem.PredictTotalDamage（挟撃・ガード込み。AI評価と将来の予測UIの共有窓口）
- 武装無しユニットには攻撃コマンドを出さない（CanAttack が false）
- データは ScriptableObject 中心（UnitData / WeaponData）。数値調整は作者がエディタで行う
- 座標は左下(0,0)、X右+、Y上+。グリッドは見た目(コード生成の四角)と論理データ(TileData[,])の二層
- 仮素材（色付き四角＋IMGUI）で進行中。本番素材への差し替えは後
- 能力値は7種（HP・力・魔力・技・速さ・守備・魔防）。物理=力vs守備、魔法=魔力vs魔防（地形防御は両方に加算）
- 兵種の階層: 歩兵／騎乗兵（騎兵・飛行兵）。輸送隊は騎兵の一種（2026-07-11 合意）。
  騎兵向けルールは UnitClass.IsCavalry()（騎兵+輸送隊）、騎乗全体は IsMounted() を使う
- Unit は Initialize で UnitData をコピーした「ランタイム能力値」を使う（Strength〜Move/Class/Weapon プロパティ経由。
  Data 直読みは Unit 内の窓口のみ。ユニット列挙は UnitRegistry（名簿）経由で、FindObjectsByType は使わない）
- 移動: 味方のマスは通過可・停止不可、敵のマスは侵入・通過不可。非アクティブ化＝盤上から外れる（占有も明け渡す）
- 操作フロー（Phase 10〜）: 移動 → 行動メニュー（攻撃/待機）→ 対象選択 のFE型。
  キャンセルは右クリック/ESC で1段階ずつ戻る（対象選択→メニュー→移動取り消し→選択解除）。
  移動の取り消しは ActionContext.RevertMove（占有も復元）。待機はメニューから明示的に選ぶ
  （旧「敵以外クリックで待機」は廃止）。メニューはユニット右隣・画面端で左に回り込み
- マスのハイライトは HighlightKind の優先度付き多層管理
  （Selection > TargetChoice > AttackRange > MoveRange > 市松模様）。色設定は GridManager に集約

### 救出システムの合意仕様（Phase 11・2026-07-11 作者合意）

- 救出: 騎乗ユニットのみ。対象は隣接する同陣営の歩兵（騎乗・輸送隊は被救出不可）。
  行動済みの味方も救出できる。救出後は「使っていない移動力分の再移動 → 待機」のみ
- 救出中のペナルティなし。救出中でも攻撃可能。救出と同一行動での「降ろす」は不可
- 降ろす: 隣接する空きマスへ再配置。降ろされたユニットはそのターン行動済み。
  降ろしたら（代わりに降ろすも含め）そのユニットの行動は終了
- 引き受け: 空きのある騎乗ユニットが、隣の救出中の味方から貨物をもらう。
  直後にできるのは「降ろす/待機」のみ（引き受け自体では行動済みにならない）
- 代わりに降ろす: 歩兵専用。貨物が歩兵で「貨物の移動力 ≧ 自分の移動力」のときのみ
- 救出・引き受けを実行したら取り消し不能（選択解除も不可。必ず待機等で行動を終える）
- 運び手が倒されたら、貨物はその場（死亡マス）に降ろされて生存
- 格納中のユニットは盤上から除外（占有明け渡し・敵AIの対象外・GetUnits に出ない）が、
  生存数（CountAlive）には数える → 「救出中の騎兵＋格納歩兵」だけでも敗北しない
- 可否判定は RescueRules に一元化（CombatRules と同じ方針）

### 輸送隊の合意仕様（Phase 12・2026-07-11 作者合意）

- 輸送隊は容量4。騎乗ユニット（騎兵・飛行兵）も救出・引き受けできる（通常の騎乗兵は歩兵のみ）
- 誰も輸送隊を救出できない。輸送隊自身も他へ格納されない（乗り込みも不可）
- 入れ子救出は禁止：貨物を持っているユニットは救出・乗り込みの対象にならない（合意(a)）
- 乗り込む：輸送隊に隣接した歩兵・騎乗兵が自分から格納される。
  自分は行動終了、輸送隊の行動は消費しない
- 貨物が複数のとき、降ろす・引き受け・代わりに降ろすは貨物リスト（CargoListMenu）から
  対象を選ぶ。1体だけなら自動選択でリストは出ない
- 引き受けの種別制限：通常の騎乗兵が受け取れるのは歩兵の貨物のみ。輸送隊は何でも受け取れる
- **味方の輸送隊が倒されたら敗北（ゲームオーバー）**。貨物の有無は問わない。
  FE上級者向けに自由度より機能・挙動を守る方針（作者）。
  通常の騎乗兵の死亡は従来どおり（貨物はその場に降ろされて生存、ゲームは続く）
- 輸送隊は当面プレイヤー専用（敵AIは救出系を使わないため。合意(c)）
- 輸送隊は武器なしユニット（攻撃コマンドが出ない。Phase 9 の武装無し分岐の実地例）

### Phase 8 以降の追加合意（2026-07-08）

- 武器分類: 剣・斧・槍＝前衛武器、弓・魔法＝後衛武器（WeaponData にカテゴリ欄を追加、データ駆動）
- 新しい三すくみ補正（Phase 9 で移行）: 有利側が攻撃 = +max(2, 攻撃側の技 − 防御側の速さ)、
  有利側が防御 = −max(2, 防御側の速さ − 攻撃側の技)。旧 ±1 固定は廃止
- 敵AIは新戦闘ルールに追随させる。救出・飛翔コマンドは当面敵は使わない
- メニューUIは IMGUI で簡易実装（uGUI 化は素材差し替え時期にまとめて）
- 必中（RNGなし）・反撃なし（CanCounter フック維持）・ScriptableObject データ駆動は継続
- 育成要素（レベル・経験値・成長率）はロードマップ外（将来枠）

## コード構成（Assets/_Game/Scripts/）

- Core/ … BattleController(操作の司令塔・7状態FSM: Idle/MoveSelect/CommandMenu/TargetSelect/UnitTargetSelect/TileSelect/CargoSelect),
  ActionContext(1行動の文脈・移動取り消し・再移動予算・取り消し不能点Commit), TurnManager(フェイズ・勝敗・簡易UI), BattleSetup(StageDataを読んで初期配置)
- Grid/ … GridManager(盤面・座標変換・多層ハイライト・HighlightKind), TileData(1マスの論理データ), MovementCalculator(BFS移動範囲・味方すり抜け)
- UI/ … ActionMenu(IMGUI行動メニュー。Entryリスト駆動・BattleControllerが自動生成),
  CargoListMenu(貨物リスト選択。名前+HP表示・BattleControllerが自動生成)
- Units/ … Unit(盤上のユニット・ランタイム能力値・行動済み・格納Carried/IsCarried), UnitClass(兵種enum+IsMounted),
  RescueRules(救出系コマンドの可否判定一元化・乗り込む判定・死亡時配置先探索FindReleaseCells),
  UnitRegistry(全ユニットの名簿・輸送隊死亡フラグPlayerTransporterLost), Faction(陣営enum)
- Combat/ … CombatSystem(攻撃解決・挟撃・ガード・反撃フック・PredictTotalDamage), CombatRules(射程・三すくみ・挟撃可否の一元判定),
  DamageCalculator(物理/魔法分岐・DamageBreakdown内訳), WeaponType, WeaponCategory(前衛/後衛)
- Data/ … UnitData(7能力値+兵種), WeaponData, StageData(初期配置) (ScriptableObject定義)
- AI/ … EnemyAI(点数評価型)
- Editor/ … UnitDataEditor, WeaponDataEditor(Inspectorの日本語表示。エディタ専用・ビルド非含有)

アセット実体: Assets/_Game/Data/
（Unit_Player, Unit_Enemy, Unit_Player_Archer, Unit_Player_Mage, Unit_Player_Cavalry,
  Unit_Player_Transport, Unit_Enemy_Lancer, Unit_Enemy_Archer,
  W_Sword, W_Axe, W_Lance, W_Bow, W_Tome, Stage_Test）
シーン: Assets/_Game/Scenes/BattleScene.unity

## 次にやること

ロードマップ（上記プランファイル）に従って進行:
- Phase 13: 地形 ← 次はここ（TerrainType/TerrainTable・文字マップ・移動コスト・地形防御・色分け）
- Phase 14: 空中移動（飛翔） → Phase 15: 統合仕上げ

## 環境・運用の注意

- Unity は Windows 側で作者が操作。Claude は WSL からファイル編集と git のみ。
  Play モード確認は作者に依頼する（手順を番号付きで丁寧に案内する）
- シーン変更後は作者に Ctrl+S 保存を依頼してから git の状態を確認する
- 新 Input System のみ有効（activeInputHandler=1）。クリックは Mouse.current を使う
- 各フェーズ完了ごとに動作確認→コミットの小刻み進行。コミットメッセージは日本語
- Assets/_Recovery/ は Unity の自動生成物で .gitignore 済み
- 新規アセット追加時は Claude が .asset と .meta（GUID自動生成）をペアで作成する。
  .asset の YAML 編集は Unity を閉じた状態で行うのが安全（作者に事前確認する）
