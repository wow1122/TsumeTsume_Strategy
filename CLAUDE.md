# TsumeTsume_Strategy

ファイアーエムブレムif暗夜をモデルにした 2D タクティクスSRPG。Unity 6 (6000.3.10f1) + URP。
作者はプログラミングほぼ初心者。日本語で、専門用語をかみ砕いて丁寧に説明すること。絵文字は使わない。

## 進捗状況（2026-07-09 時点）

バーティカルスライス完成（Phase 0〜7）に続き、Phase 8（データ基盤刷新）完了（2026-07-09）。
7能力値・兵種・ユニットレジストリ・味方すり抜け・StageData化まで導入済み。
次は Phase 9（前衛・後衛武器と新三すくみ、CombatRules による判定一元化）。

全体計画（Phase 8〜15 ロードマップ）は
`/home/wawo/.claude/plans/tsumetsume-strategy-phase8-tsumetsume-st-elegant-parasol.md` 参照（WSL側）。
ゲーム根幹の仕様は `docs/コアシステム仕様書.md`（リポジトリ側を正とする。Desktop の原本は作者の編集用）。

## 確定済みの仕様（作者との合意。勝手に変えない）

- 命中は必中（命中・回避RNGなし）。ダメージ = max(0, 攻撃+武器威力+三すくみ補正-(防御+地形防御))
- 三すくみ: 剣>斧>槍>剣（補正は DamageCalculator.WeaponTriangleBonus = ±1）。弓・魔法は中立
- 反撃は基本なし。CombatSystem.CanCounter() フックが唯一の拡張点（現状常に false）
- 挟撃: 近接攻撃時、敵を挟んだ反対側の隣マス(上下左右)に味方がいれば追加攻撃。
  参加した味方は行動を消費しない。遠距離では不成立
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
- Combat/ … CombatSystem(攻撃解決・挟撃・反撃フック), DamageCalculator(物理/魔法分岐), WeaponType
- Data/ … UnitData(7能力値+兵種), WeaponData, StageData(初期配置) (ScriptableObject定義)
- AI/ … EnemyAI(点数評価型)
- Editor/ … UnitDataEditor(Inspectorの日本語表示。エディタ専用・ビルド非含有)

アセット実体: Assets/_Game/Data/（Unit_Player, Unit_Enemy, W_Sword, W_Axe, Stage_Test）
シーン: Assets/_Game/Scenes/BattleScene.unity

## 次にやること

ロードマップ（上記プランファイル）に従って進行:
- Phase 9: 前衛・後衛武器と新三すくみ（WeaponCategory・CombatRules 新設、弓/魔法ユニット追加）← 次はここ
- Phase 10: コマンドメニューUI・移動取り消し → Phase 11: 救出 → Phase 12: 輸送隊
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
