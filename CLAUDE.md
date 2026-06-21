# TsumeTsume_Strategy

ファイアーエムブレムif暗夜をモデルにした 2D タクティクスSRPG。Unity 6 (6000.3.10f1) + URP。
作者はプログラミングほぼ初心者。日本語で、専門用語をかみ砕いて丁寧に説明すること。絵文字は使わない。

## 進捗状況（2026-06-12 時点）

バーティカルスライス完成（Phase 0〜7 すべて完了）。
戦闘マップ1枚で「選択→移動→攻撃→敵フェイズ→勝敗」の中核ループが動く。

全体計画は `/home/wawo/.claude/plans/ancient-gathering-sketch.md` 参照（WSL側）。

## 確定済みの仕様（作者との合意。勝手に変えない）

- 命中は必中（命中・回避RNGなし）。ダメージ = max(0, 攻撃+武器威力+三すくみ補正-(防御+地形防御))
- 三すくみ: 剣>斧>槍>剣（補正は DamageCalculator.WeaponTriangleBonus = ±1）。弓・魔法は中立
- 反撃は基本なし。CombatSystem.CanCounter() フックが唯一の拡張点（現状常に false）
- 挟撃: 近接攻撃時、敵を挟んだ反対側の隣マス(上下左右)に味方がいれば追加攻撃。
  参加した味方は行動を消費しない。遠距離では不成立
- データは ScriptableObject 中心（UnitData / WeaponData）。数値調整は作者がエディタで行う
- 座標は左下(0,0)、X右+、Y上+。グリッドは見た目(コード生成の四角)と論理データ(TileData[,])の二層
- 仮素材（色付き四角＋IMGUI）で進行中。本番素材への差し替えは後

## コード構成（Assets/_Game/Scripts/）

- Core/ … BattleController(操作の司令塔・状態機械), TurnManager(フェイズ・勝敗・簡易UI), BattleSetup(初期配置)
- Grid/ … GridManager(盤面・座標変換・ハイライト), TileData(1マスの論理データ), MovementCalculator(BFS移動範囲)
- Units/ … Unit(盤上のユニット・HP・行動済み), Faction(陣営enum)
- Combat/ … CombatSystem(攻撃解決・挟撃・反撃フック), DamageCalculator, WeaponType
- Data/ … UnitData, WeaponData (ScriptableObject定義)

アセット実体: Assets/_Game/Data/（Unit_Player, Unit_Enemy, W_Sword, W_Axe）
シーン: Assets/_Game/Scenes/BattleScene.unity

## 環境・運用の注意

- Unity は Windows 側で作者が操作。Claude は WSL からファイル編集と git のみ。
  Play モード確認は作者に依頼する（手順を番号付きで丁寧に案内する）
- シーン変更後は作者に Ctrl+S 保存を依頼してから git の状態を確認する
- 新 Input System のみ有効（activeInputHandler=1）。クリックは Mouse.current を使う
- 各フェーズ完了ごとに動作確認→コミットの小刻み進行。コミットメッセージは日本語
- Assets/_Recovery/ は Unity の自動生成物で .gitignore 済み

## 次にやる候補（未着手）

- 地形（森・山・壁: 移動コスト・防御補正・見た目の色分け）
- 敵AIの改良（待ち伏せ型、ボス、優先目標）
- 味方の上の通過（現在は占有マスを通れない簡易仕様）
- 弓・魔法ユニットの追加（射程2の武器は仕組み上もう動く）
- マップを正式にデザイン、ユニット数を増やす
- 育成・クラス・支援会話・拠点・ストーリー（スライス後の大型要素）
