# TsumeTsume_Strategy

ファイアーエムブレムif暗夜をモデルにした 2D タクティクスSRPG。Unity 6 (6000.3.10f1) + URP。
作者はプログラミングほぼ初心者。日本語で、専門用語をかみ砕いて丁寧に説明すること。絵文字は使わない。

## 進捗状況（2026-07-09 時点）

バーティカルスライス完成（Phase 0〜7）、Phase 8（データ基盤刷新）完了に続き、
Phase 9（前衛・後衛武器と新三すくみ、CombatRules による判定一元化）を実装済み（作者の Play 確認待ち）。
次は Phase 10（コマンドメニューUI・移動取り消し）。

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
- Unit は Initialize で UnitData をコピーした「ランタイム能力値」を使う（Strength〜Move/Class/Weapon プロパティ経由。
  Data 直読みは Unit 内の窓口のみ。ユニット列挙は UnitRegistry（名簿）経由で、FindObjectsByType は使わない）
- 移動: 味方のマスは通過可・停止不可、敵のマスは侵入・通過不可。非アクティブ化＝盤上から外れる（占有も明け渡す）

### Phase 8 以降の追加合意（2026-07-08）

- 武器分類: 剣・斧・槍＝前衛武器、弓・魔法＝後衛武器（WeaponData にカテゴリ欄を追加、データ駆動）
- 新しい三すくみ補正（Phase 9 で移行）: 有利側が攻撃 = +max(2, 攻撃側の技 − 防御側の速さ)、
  有利側が防御 = −max(2, 防御側の速さ − 攻撃側の技)。旧 ±1 固定は廃止
- 敵AIは新戦闘ルールに追随させる。救出・飛翔コマンドは当面敵は使わない
- メニューUIは IMGUI で簡易実装（uGUI 化は素材差し替え時期にまとめて）
- 必中（RNGなし）・反撃なし（CanCounter フック維持）・ScriptableObject データ駆動は継続
- 育成要素（レベル・経験値・成長率）はロードマップ外（将来枠）

## コード構成（Assets/_Game/Scripts/）

- Core/ … BattleController(操作の司令塔・状態機械), TurnManager(フェイズ・勝敗・簡易UI), BattleSetup(StageDataを読んで初期配置)
- Grid/ … GridManager(盤面・座標変換・ハイライト), TileData(1マスの論理データ), MovementCalculator(BFS移動範囲・味方すり抜け)
- Units/ … Unit(盤上のユニット・ランタイム能力値・行動済み), UnitClass(兵種enum+IsMounted), UnitRegistry(全ユニットの名簿), Faction(陣営enum)
- Combat/ … CombatSystem(攻撃解決・挟撃・ガード・反撃フック・PredictTotalDamage), CombatRules(射程・三すくみ・挟撃可否の一元判定),
  DamageCalculator(物理/魔法分岐・DamageBreakdown内訳), WeaponType, WeaponCategory(前衛/後衛)
- Data/ … UnitData(7能力値+兵種), WeaponData, StageData(初期配置) (ScriptableObject定義)
- AI/ … EnemyAI(点数評価型)
- Editor/ … UnitDataEditor, WeaponDataEditor(Inspectorの日本語表示。エディタ専用・ビルド非含有)

アセット実体: Assets/_Game/Data/
（Unit_Player, Unit_Enemy, Unit_Player_Archer, Unit_Player_Mage, Unit_Enemy_Lancer, Unit_Enemy_Archer,
  W_Sword, W_Axe, W_Lance, W_Bow, W_Tome, Stage_Test）
シーン: Assets/_Game/Scenes/BattleScene.unity

## 次にやること

ロードマップ（上記プランファイル）に従って進行:
- Phase 9: 実装済み・Play確認待ち。確認OKなら日本語メッセージでコミット
- Phase 10: コマンドメニューUI・移動取り消し ← 次はここ → Phase 11: 救出 → Phase 12: 輸送隊
- Phase 13: 地形 → Phase 14: 空中移動（飛翔） → Phase 15: 統合仕上げ（Phase 13 は前倒し可）

## 環境・運用の注意

- Unity は Windows 側で作者が操作。Claude は WSL からファイル編集と git のみ。
  Play モード確認は作者に依頼する（手順を番号付きで丁寧に案内する）
- シーン変更後は作者に Ctrl+S 保存を依頼してから git の状態を確認する
- 新 Input System のみ有効（activeInputHandler=1）。クリックは Mouse.current を使う
- 各フェーズ完了ごとに動作確認→コミットの小刻み進行。コミットメッセージは日本語
- Assets/_Recovery/ は Unity の自動生成物で .gitignore 済み
- 新規アセット追加時は Claude が .asset と .meta（GUID自動生成）をペアで作成する。
  .asset の YAML 編集は Unity を閉じた状態で行うのが安全（作者に事前確認する）
